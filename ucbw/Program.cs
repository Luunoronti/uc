using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace ucbw
{
    internal class Program
    {
        private static PerformanceCounter cpuCounter;
        private static PerformanceCounter ramCounter;
        private static bool perfCountersMade;

        private const int MSG_SIZE = 1  // msg type (0x10: progress message)
                        + 1 // progress value (0-100)
                        + 1 // CPU percent (0-100)
                        + 1 // RAM percent (0-100)
                        + 1 // currTitle size (0-255)
                        + 1 // currMsg size (0-255)/ if msg or title is larger, substring it
            ;

        static void Main(string[] args)
        {
            if (args.Length < 5)
            {
                return;
            }

            const int SB_LEN = 256;
            var host = args.First(a => a.StartsWith("-host:")).Substring(6);
            var port = int.Parse(args.First(a => a.StartsWith("-port:")).Substring(6));
            var localhost = args.First(a => a.StartsWith("-lhost:")).Substring(7);
            var localport = int.Parse(args.First(a => a.StartsWith("-lport:")).Substring(7));
            var pid = int.Parse(args.First(a => a.StartsWith("-pid:")).Substring(5));


            var sb = new StringBuilder(SB_LEN);

            var udp = new UdpClient { ExclusiveAddressUse = false };
            udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            udp.Client.Bind(new IPEndPoint(IPAddress.Parse(localhost), localport));

            new Thread(() =>
            {
                cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                ramCounter = new PerformanceCounter("Memory", "Available MBytes");
                perfCountersMade = true;
            }).Start();

            while (true)
            {
                string lastTitle = "";
                string lastMsg = "";
                int lastPercentage = -1;

                var titleSB = new StringBuilder(SB_LEN);
                var msgSB = new StringBuilder(SB_LEN);

                IntPtr hwnd = GetProgressWindow(pid);
                if (hwnd == IntPtr.Zero)
                {
                    byte[] data = new byte[1] { 0x11 };
                    udp.Send(data, data.Length, new IPEndPoint(IPAddress.Parse(host), port));
                    return;
                }

                (var labelHwnd, var progressHwnd) = GetTaskLabelAndProgressWindow(hwnd);

                while (true)
                {
                    var currTitle = GetWindowTextInt(hwnd);
                    var currMsg = GetWindowTextInt(labelHwnd);
                    var percent = GetPercentage(progressHwnd);

                    if (currTitle == null || currMsg == null) break;

                    if (lastTitle == currTitle && lastMsg == currMsg && lastPercentage == percent)
                    {
                        Thread.Sleep(1);
                        continue;
                    }
                    lastTitle = currTitle;
                    lastMsg = currMsg;
                    lastPercentage = percent;


                    // we now implement new approach: sending byte[] buffer instead of json
                    // to make stuff way faster on both client and sender

                    int msgbytes = Encoding.UTF8.GetByteCount(currMsg);
                    while (msgbytes > 255)
                    {
                        currMsg = currMsg.Substring(0, currMsg.Length - 1);
                        msgbytes = Encoding.UTF8.GetByteCount(currMsg);
                    }
                    var titleBytes = Encoding.UTF8.GetByteCount(currTitle);
                    while (titleBytes > 255)
                    {
                        currTitle = currTitle.Substring(0, currTitle.Length - 1);
                        titleBytes = Encoding.UTF8.GetByteCount(currTitle);
                    }

                    byte[] data = new byte[MSG_SIZE + titleBytes + msgbytes];
                    data[0] = 0x10;
                    data[1] = (byte)percent;
                    if (perfCountersMade)
                    {
                        data[2] = (byte)(int)cpuCounter.NextValue();
                        data[3] = (byte)(int)((totalMemMB - ramCounter.NextValue()) / totalMemMB * 100);
                    }

                    data[4] = (byte)titleBytes;
                    data[5] = (byte)msgbytes;

                    Encoding.UTF8.GetBytes(currTitle, 0, currTitle.Length, data, 6);
                    Encoding.UTF8.GetBytes(currMsg, 0, currMsg.Length, data, 6 + titleBytes);
                    
                    udp.Send(data, data.Length, new IPEndPoint(IPAddress.Parse(host), port));

                    Thread.Sleep(1);
                }
            }

            int GetPercentage(IntPtr hwnd)
            {
                var pbm = SendMessage(hwnd, PBM_GETPOS, IntPtr.Zero, IntPtr.Zero);
                //var rangeLow = 0; SendMessage(progressHwnd, PBM_GETRANGE, (IntPtr)1, IntPtr.Zero);
                var rangeHigh = SendMessage(hwnd, PBM_GETRANGE, IntPtr.Zero, IntPtr.Zero);
                if (rangeHigh == 0) rangeHigh = 1000; // set as default
                return (int)(((float)pbm / (float)rangeHigh) * 100.0f);
            }
            string GetWindowTextInt(IntPtr hwnd)
            {
                if (0 == GetWindowText(hwnd, sb, SB_LEN))
                    return null;
                return sb.ToString();
            }
        }

        private static bool ReadConfigFromFileInCurrenFolder(out IPEndPoint ep)
        {
            try
            {
                var spl = File.ReadAllLines(Environment.CurrentDirectory + "/editorconsole.cfg")[0].Split(':');
                ep = new IPEndPoint(IPAddress.Parse(spl[0]), int.Parse(spl[1]));
                return true;
            }
            catch
            {
                ep = null;
                return false;
            }
        }

        private static IntPtr GetProgressWindow(int pid)
        {
            var sb = new StringBuilder(128);
            IntPtr fhwnd = IntPtr.Zero;
            var sw = Stopwatch.StartNew();
            while (fhwnd == IntPtr.Zero)
            {
                _ = EnumWindows(new CallBackPtr((hwnd, lParam) =>
                {
                    _ = GetWindowThreadProcessId(hwnd, out var procId);

                    if (procId != pid)
                        return true;

                    if (0 == GetClassName(hwnd, sb, 128))
                        return true;
                    var c = sb.ToString();
                    if (c != CLASS_NAME)
                        return true;

                    fhwnd = hwnd;
                    return false;
                }), 0);
                if (fhwnd == IntPtr.Zero)
                {
                    if (sw.Elapsed.TotalSeconds > 2)
                        return IntPtr.Zero;
                    Thread.Sleep(100);
                }
            }
            return fhwnd;
        }

        private static long totalMemMB = (GC.GetTotalMemory(false)) / 1024 / 1024;
        private static (IntPtr, IntPtr) GetTaskLabelAndProgressWindow(IntPtr hwndParent)
        {
            // in theory, first window is progress and last control is Static
            // we'll go with that for now
            var list = new List<IntPtr>();
            _ = EnumChildWindows(hwndParent, (hwnd, lParam) =>
            {
                list.Add(hwnd);
                return true;
            }, IntPtr.Zero);
            if (list.Count == 0) return (IntPtr.Zero, IntPtr.Zero);
            return (list.Last(), list.First());
        }


        public const string CLASS_NAME = "#32770";
        public const string PB_CLASS_NAME = "msctls_progress32";

        public delegate bool CallBackPtr(IntPtr hwnd, int lParam);
        [DllImport("user32.dll")] private static extern int EnumWindows(CallBackPtr callPtr, int lPar);
        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);
        [DllImport("user32.dll")] private static extern int GetWindowThreadProcessId(IntPtr hWnd, out int lpdwProcessId);
        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern int GetClassName(IntPtr hWnd, StringBuilder lpString, int nMaxCount);
        [DllImport("user32")][return: MarshalAs(UnmanagedType.Bool)] private static extern bool EnumChildWindows(IntPtr window, CallBackPtr callback, IntPtr lParam);
        [DllImport("user32.dll")] private static extern int SendMessage(IntPtr hWnd, int wMsg, IntPtr wParam, IntPtr lParam);

        public const int PBM_GETPOS = 0x0408;
        public const int PBM_GETRANGE = 0x0407;


        public class EC_CommandStart { public string Command { get; set; } }
        public enum EC_Type { CommandCompleted = 0, Message = 1, Ping = 2, Progress = 3 }

        public class EC_Base { public EC_Type Type { get; set; } }
        public class EC_CommandCompleted : EC_Base { }
        public class EC_Message : EC_Base { public string Message { get; set; } }
        public class EC_Ping : EC_Base { }
        public class EC_Progress : EC_Base { public string Message { get; set; } public string Title { get; set; } public int Progress { get; set; } public int CPU { get; set; } public int RAM { get; set; } public bool Visible { get; set; } }


    }
}

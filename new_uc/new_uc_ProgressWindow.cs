using Newtonsoft.Json;
using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;

internal partial class Program
{
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

    private static long totalMemMB = (GC.GetGCMemoryInfo().TotalAvailableMemoryBytes) / 1024 / 1024;
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


    private static PerformanceCounter cpuCounter;
    private static PerformanceCounter ramCounter;
    private static bool perfCountersMade;

    private static bool ProcessUnityBusyWindowLookup(string[] args)
    {
        const int SB_LEN = 256;
        const string AWAIT_MSG = "Awaiting progress...";

        if (!args.Any(a => a == "unityBusyWindow")) return false;
        var host = args.First(a => a.StartsWith("-host:"))[6..];
        var port = int.Parse(args.First(a => a.StartsWith("-port:"))[6..]);
        var localhost = args.First(a => a.StartsWith("-lhost:"))[7..];
        var localport = int.Parse(args.First(a => a.StartsWith("-lport:"))[7..]);
        var pid = int.Parse(args.First(a => a.StartsWith("-pid:"))[5..]);

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
            //int lastPercentage = -1;

            var titleSB = new StringBuilder(SB_LEN);
            var msgSB = new StringBuilder(SB_LEN);

            IntPtr hwnd = GetProgressWindow(pid);
            if (hwnd == IntPtr.Zero)
            {
                SendMsgToTerminal_Pr(new EC_Progress { Type = EC_Type.Progress, Visible = false });
                return true;
            }

            (var labelHwnd, var progressHwnd) = GetTaskLabelAndProgressWindow(hwnd);

            while (true)
            {
                var currTitle = GetWindowTextInt(hwnd);
                var currMsg = GetWindowTextInt(labelHwnd);
                var percent = GetPercentage(progressHwnd);

                if (currTitle == null || currMsg == null) break;

                if (lastTitle == currTitle && lastMsg == currMsg)
                {
                    Thread.Sleep(1);
                    continue;
                }
                lastTitle = currTitle;
                lastMsg = currMsg;

                if (perfCountersMade)
                {
                    var curr = totalMemMB - ramCounter.NextValue();
                    SendMsgToTerminal_Pr(new EC_Progress { Type = EC_Type.Progress, Progress = percent, Message = currMsg, Title = currTitle, Visible = true, RAM = (int)((curr / totalMemMB) * 100), CPU = (int)cpuCounter.NextValue() });
                }
                else
                {
                    SendMsgToTerminal_Pr(new EC_Progress { Type = EC_Type.Progress, Progress = percent, Message = currMsg, Title = currTitle, Visible = true, RAM = 0, CPU = 0 });
                }

                Thread.Sleep(1);
            }

            SendMsgToTerminal_Pr(new EC_Progress { Type = EC_Type.Progress, Visible = false });
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
        void SaveCursorPos() => SendMsgToTerminal("\u001b7");
        void RestoreCursorPos() => SendMsgToTerminal("\u001b8");
        void ClearChars(string og) => SendMsgToTerminal("".PadLeft(og.Length));
        void SendMsgToTerminal(string message)
        {
            var data = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new EC_Message { Type = EC_Type.Message, Message = message }));
            udp.Send(data, data.Length, new IPEndPoint(IPAddress.Parse(host), port));
        }
        void SendMsgToTerminal_Pr(EC_Progress progress)
        {
            var data = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(progress));
            udp.Send(data, data.Length, new IPEndPoint(IPAddress.Parse(host), port));
        }
    }




    private static bool progressBegan = false;
    private static int progress_BeforeX;
    private static int progress_BeforeY;

    private static int progress_ConY1 = 0;
    private static int progress_ConY2 = 0;

    private static string progress_LastTitle = "";
    private static string progress_LastMsg = "";

    private static void HideProgress()
    {
        if (!progressBegan) return;
        progressBegan = false;

        // we must clear everything
        Console.CursorLeft = 0;
        Console.CursorTop = progress_ConY1;
        Console.Write("".PadLeft(Console.WindowWidth));

        Console.CursorLeft = 0;
        Console.CursorTop = progress_ConY2;
        Console.Write("".PadLeft(Console.WindowWidth));

        Console.CursorLeft = progress_BeforeX;
        Console.CursorTop = progress_BeforeY;
        Console.CursorVisible = true;
    }
    private static void ShowProgress(string title, string message, int progress, int cpu, int ram)
    {
        if (progressBegan == false)
        {
            // if we don't have anough space, push everything up
            var st = Console.CursorTop;
            while (st >= Console.WindowHeight - 5)
            {
                Console.WriteLine();
                st--;
            }
            Console.CursorTop = st;


            progress_BeforeX = Console.CursorLeft;
            progress_BeforeY = Console.CursorTop;

            progress_ConY1 = progress_BeforeY;
            if (progress_BeforeX != 0)
            {
                Console.WriteLine();
                progress_ConY1 = progress_BeforeY + 1;
            }
            progress_ConY2 = progress_ConY1 + 1;

            progressBegan = true;
            Console.CursorVisible = false;

            // also, make sure we have space
            Console.CursorTop = progress_ConY1;
        }


        var ttl = title;
        var msg = message;
        if (title.Length < progress_LastTitle.Length) ttl = title.PadRight(progress_LastTitle.Length);
        if (message.Length < progress_LastMsg.Length) msg = message.PadRight(progress_LastMsg.Length);

        progress_LastTitle = title;
        progress_LastMsg = message;

        var perStr = $"\u001b[33m[\u001b[32m{"".PadRight(progress / 5, '█').PadRight(20, '-')}\u001b[33m]\u001b[32m {progress,3}%\u001b[0m";

        Console.CursorLeft = 0;
        Console.CursorTop = progress_ConY1;

        Console.Write($"{perStr} \u001b[1m\u001b[93m{ttl}\u001b[0m");

        Console.CursorLeft = 0;
        Console.CursorTop = progress_ConY2;

        var cpuFlag = cpu > 40 ? (cpu > 80 ? "\u001b[31m" : "\u001b[33m") : "\u001b[32m";
        var ramFlag = ram > 40 ? (ram > 80 ? "\u001b[31m" : "\u001b[33m") : "\u001b[32m";

        var str = $"CPU:{cpuFlag}{cpu,3}%\u001b[0m  RAM:{ramFlag}{ram,3}%\u001b[0m";
        Console.Write($"{str,-42}{msg}");

    }



}




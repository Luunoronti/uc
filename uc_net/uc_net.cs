using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace uc_net
{
    internal partial class Program
    {
        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("Unity Command tool v. 0.2");
                Console.WriteLine("Usage:");
                Console.WriteLine("\t'uc <command> <parameters> <flags>' sends command to default host editor");
                Console.WriteLine("\t'uc list commands' shows a list of all available commands");
                Console.WriteLine("\t'uc help' shows help page");
                Console.WriteLine("\t'uc selfupdate' to download and update uc");

                Console.WriteLine();

                return;
            }
                

            if (TestProgress(args))
                return;

            try
            {
                var spl = File.ReadAllLines(Environment.CurrentDirectory + "/editorconsole.cfg")[0].Split(':');
                IPEndPoint commandEp = new IPEndPoint(IPAddress.Parse(spl[0]), int.Parse(spl[1]));

                ProcessStandard(args, commandEp);
            }
            catch
            {
                return;
            }
        }

        //private static bool ReadConfigFromCmdLine(string[] args, out IPEndPoint ep, out IPEndPoint statusEp)
        //{
        //    if (args.Length < 3)
        //    {
        //        ep = null;
        //        statusEp = null;
        //        return false;
        //    }

        //    var ips = args.FirstOrDefault(a => a.StartsWith("-host:"))?.Substring(6) ?? "";
        //    var ports = args.FirstOrDefault(a => a.StartsWith("-port:"))?.Substring(6) ?? "";
        //    var sports = args.FirstOrDefault(a => a.StartsWith("-statusPort:"))?.Substring(12) ?? "";

        //    if (string.IsNullOrEmpty(ips) || string.IsNullOrEmpty(ports) || string.IsNullOrEmpty(sports))
        //    {
        //        ep = null;
        //        statusEp = null;
        //        return false;
        //    }

        //    if (!IPAddress.TryParse(ips, out var ip)
        //        || !int.TryParse(ports, out var port)
        //        || !int.TryParse(sports, out var statusPort))
        //    {
        //        ep = null;
        //        statusEp = null;
        //        return false;
        //    }

        //    ep = new IPEndPoint(ip, port);
        //    statusEp = new IPEndPoint(ip, statusPort);

        //    return true;
        //}

        //private static bool IsSysArg(string arg)
        //    => arg.StartsWith("-host:")
        //        || arg.StartsWith("-port:")
        //        || arg.StartsWith("-statusPort:");
        private static void SendArgsCommand(string[] args, UdpClient udp)
        {
            var command = string.Join(" ", args/*.Where(a => !IsSysArg(a))*/.Select(a => a.Any(c => char.IsWhiteSpace(c)) ? $"\"{a}\"" : a));
            // command buffer is now:
            // this is in preparation for RUST version later
            // 1 byte command type (0x02)
            // 2 bytes command len
            // x bytes command
            var cmdLen = Encoding.UTF8.GetByteCount(command);
            var data = new byte[cmdLen + 3];
            data[0] = 0x02;
            data[1] = (byte)((cmdLen >> 8) & 0xFF);
            data[2] = (byte)((cmdLen) & 0xFF);

            Encoding.UTF8.GetBytes(command, 0, command.Length, data, 3);
            udp.Send(data, data.Length);
        }

        private static bool ProcessMessage(UdpClient udp)
        {
            if (udp.Available <= 0)
                return true;

            IPEndPoint ep = default;
            var data = udp.Receive(ref ep);
            if (data == null || data.Length == 0)
            {
                Console.Write("[eb err]");
                return false;
            }

            switch (data[0])
            {
                case 0x01:     // command completed
                    return false;
                case 0x11:     // hide progress
                    HideProgress();
                    return true;
                case 0x10:     // progress
                    ShowProgress(Encoding.UTF8.GetString(data, 6, data[4]), Encoding.UTF8.GetString(data, 6 + data[4], data[5]), data[1], data[2], data[3]);
                    return true;
                case 0xF0:     // text message to show on screen
                    Console.Write(Encoding.UTF8.GetString(data, 1, data.Length - 1));
                    return true;
                case 0x30:      // Ping
                    return true;
                default:
                    return true;
            }
        }
        private static void ProcessStandard(string[] args, IPEndPoint comandEp)
        {
            UdpClient udp;
            udp = new UdpClient { ExclusiveAddressUse = false };
            udp.Connect(comandEp);
            SendArgsCommand(args, udp);
            while (ProcessMessage(udp))
                Thread.Sleep(1);
        }

     
        


        private static bool TestProgress(string[] args)
        {
            if (args.Length != 1) return false;

            if (args.Any(a => a == "testProgress") == false) return false;

            Console.Write("testing progress: ");
            var rnd = new Random();
            while (true)
            {
                if (rnd.Next(10) < 2)
                {
                    HideProgress();
                    Console.WriteLine("New line here");
                }
                else
                {
                    var title = GenerateString(rnd, rnd.Next(20));
                    var msg = GenerateString(rnd, rnd.Next(80));
                    ShowProgress(title, msg, rnd.Next(100), rnd.Next(100), rnd.Next(100));
                }


                Thread.Sleep(500 + new Random().Next(1000));

            }

            static string GenerateString(Random rng, int length)
            {
                char[] letters = new char[length];
                for (int i = 0; i < length; i++)
                {
                    letters[i] = (char)(rng.Next('A', 'Z' + 1));
                }
                return new string(letters);
            }
        }



    }



}

using Newtonsoft.Json;
using Pastel;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics.Arm;
using System.Text;
using static System.Net.Mime.MediaTypeNames;
internal partial class Program
{
    public class EditorConsoleConfig { public string? ip { get; set; } public int port { get; set; } public int statusport { get; set; } }

    public class EC_CommandStart { public string? Command { get; set; } }
    public enum EC_Type { CommandCompleted = 0, Message = 1, Ping = 2, Progress = 3 }

    public class EC_Base { public EC_Type Type { get; set; } }
    public class EC_CommandCompleted : EC_Base { }
    public class EC_Message : EC_Base { public string? Message { get; set; } }
    public class EC_Ping : EC_Base { }
    public class EC_Progress : EC_Base { public string? Message { get; set; } public string? Title { get; set; } public int Progress { get; set; } public int CPU { get; set; } public int RAM { get; set; } public bool Visible { get; set; } }


    private static bool ShowHelpIfNoArgs(string[] args)
    {
        if (args.Length > 0) return false;
        Console.WriteLine("Unity Command tool v. 0.2");
        Console.WriteLine("Usage:");
        Console.WriteLine("\t'uc <command> <parameters> <flags>' sends command to default host editor");
        Console.WriteLine("\t'uc list commands' shows a list of all available commands");
        Console.WriteLine("\t'uc help' shows help page");

        Console.WriteLine();

        return true;
    }
    private static bool ReadConfigFromFileInCurrenFolder(out string ip, out int port, out int statusPort)
    {
        ip = "";
        port = 0;
        statusPort = 0;

        var cfgFile = Environment.CurrentDirectory + "\\editorconsole.json";
        if (!File.Exists(cfgFile))
            cfgFile = Environment.CurrentDirectory + "\\Assets\\StreamingAssets\\editorconsole.json";
        if (!File.Exists(cfgFile))
            return false;

        var cfg = JsonConvert.DeserializeObject<EditorConsoleConfig>(File.ReadAllText(cfgFile));
        ip = cfg?.ip ?? "";
        port = cfg?.port ?? -1;
        statusPort = cfg?.statusport ?? -1;

        return !string.IsNullOrEmpty(ip) && port != -1 && statusPort != -1;
    }
    private static bool ReadConfigFromCmdLine(string[] args, out string ip, out int port, out int statusPort)
    {
        ip = "";
        port = 0;
        statusPort = 0;

        if (args.Length == 0) return false;

        ip = args.FirstOrDefault(a => a.StartsWith("-host:"))?[6..] ?? "";
        _ = int.TryParse(args.FirstOrDefault(a => a.StartsWith("-port:"))?[6..] ?? "", out port);
        _ = int.TryParse(args.FirstOrDefault(a => a.StartsWith("-statusPort:"))?[12..] ?? "", out statusPort);

        return !string.IsNullOrEmpty(ip) && port != -1 && statusPort != -1;
    }
    private static string? ReceiveString(UdpClient udp)
    {
        if (udp.Available > 0)
        {
            IPEndPoint ep = default;
            var rd = udp.Receive(ref ep);
            if (rd == null || rd.Length == 0)
            {
                Console.Write("[eb err]");
                return null;
            }
            var msg = System.Text.Encoding.UTF8.GetString(rd, 0, rd.Length);
            if (msg.Length == 0)
            {
                Console.Write("[em err]");
                return null;
            }
            return msg;
        }
        return null;
    }
    private static bool ProcessStatusRequest(string[] args, string ip, int statusPort)
    {
        if (!args.Any(a => a.ToLower() == "ohmyposh"))
            return false;

        var udp2 = new UdpClient();
        udp2.Connect(new IPEndPoint(IPAddress.Parse(ip), statusPort));
        var d1 = new byte[1];
        udp2.Send(d1, d1.Length);
        var sw = Stopwatch.StartNew();
        while (true)
        {
            if (udp2.Available <= 0)
            {
                sw.Stop();
                if (sw.Elapsed.TotalSeconds > 1)
                {
                    Console.Write("[timeout]");
                    return true;
                }
                sw.Start();
                Thread.Sleep(1);
                continue;
            }
            var msg = ReceiveString(udp2);
            if (msg == null)
                return true;

            Console.Write(msg);
            return true;
        }
    }

    private static bool IsSysArg(string arg)
        => arg.StartsWith("-host:")
            || arg.StartsWith("-port:")
            || arg.StartsWith("-statusPort:");
    private static string QuotationIfWhitespaces(string input)
        => input.Any(c => char.IsWhiteSpace(c)) ? $"\"{input}\"" : input;
    private static void SendArgsCommand(string[] args, UdpClient udp, string ip, int port)
    {
        var command = string.Join(" ", args.Where(a => !IsSysArg(a)).Select(arg => QuotationIfWhitespaces(arg)));
        var cmdObject = new EC_CommandStart { Command = command };
        var cmdJson = JsonConvert.SerializeObject(cmdObject);
        var data = System.Text.Encoding.UTF8.GetBytes(cmdJson);
        udp.Send(data, data.Length);
    }

    private static EC_Base? GetMsg(string msg)
    {
        var bs = JsonConvert.DeserializeObject<EC_Base>(msg) ?? throw new Exception("Wrong message: Message object is not valid EC_Base.");
        return bs.Type switch
        {
            EC_Type.CommandCompleted => JsonConvert.DeserializeObject<EC_CommandCompleted>(msg),
            EC_Type.Message => JsonConvert.DeserializeObject<EC_Message>(msg),
            EC_Type.Ping => JsonConvert.DeserializeObject<EC_Ping>(msg),
            EC_Type.Progress => JsonConvert.DeserializeObject<EC_Progress>(msg),
            _ => throw new Exception("Wrong message: Unknown message type.")
        };
    }

    private static bool ProcessPing(UdpClient udp)
    {
        if (udp.Available != 1)
            return false;

        IPEndPoint ep = default;
        var rd = udp.Receive(ref ep);
        return rd != null && rd.Length == 1 ? throw new Exception("Error when receiving ping message") : true;
    }
    private static bool ProcessMessage(UdpClient udp)
    {
        if (udp.Available <= 0)
            return true;

        var str = ReceiveString(udp);
        if (str == null) return false;

        var msg = GetMsg(str);
        if (msg == null) return false;
        switch (msg)
        {
            case EC_Message ecmsg:
                Console.Write(ecmsg?.Message ?? "");
                return true;
            case EC_Progress ecmsgp:
                if (ecmsgp.Visible)
                    ShowProgress(ecmsgp.Title ?? "", ecmsgp.Message ?? "", ecmsgp.Progress, ecmsgp.CPU, ecmsgp.RAM);
                else
                    HideProgress();
                return true;
            case EC_Ping:
                return true;
            case EC_CommandCompleted:
                HideProgress(); // also attempt to hide progress
                return false;
            default:
                return false;
        }
    }
    private static void ProcessStandard(string[] args, string ip, int port)
    {
        UdpClient udp;
        udp = new UdpClient();

        udp.ExclusiveAddressUse = false;
        udp.Connect(new IPEndPoint(IPAddress.Parse(ip), port));

        SendArgsCommand(args, udp, ip, port);

        while (ProcessMessage(udp))
        {
            Thread.Sleep(1);
        }
    }

    public static char GenerateChar(Random rng)
    {
        // 'Z' + 1 because the range is exclusive
        return (char)(rng.Next('A', 'Z' + 1));
    }

    public static string GenerateString(Random rng, int length)
    {
        char[] letters = new char[length];
        for (int i = 0; i < length; i++)
        {
            letters[i] = GenerateChar(rng);
        }
        return new string(letters);
    }



    private static bool TestProgress(string[] args)
    {
        if (args.Any(a => a == "testProgress") == false) return false;

        var rnd = new Random();
        while (true)
        {
           // if (rnd.Next(10) < 2)
           // {
           //     HideProgress();
           //     Console.WriteLine("New line here");
           // }
           // else
            {
                var title = GenerateString(rnd, rnd.Next(20));
                var msg = GenerateString(rnd, rnd.Next(80));
                ShowProgress(title, msg, rnd.Next(100), rnd.Next(100), rnd.Next(100));
            }


            Thread.Sleep(500 + new Random().Next(1000));

        }
    }
    private static void Main(string[] args)
    {
        try
        {
            if (ShowHelpIfNoArgs(args))
                return;

            if (TestProgress(args))
                return;

            if (!ReadConfigFromCmdLine(args, out var ip, out var port, out var statusPort))
            {
                if (!ReadConfigFromFileInCurrenFolder(out ip, out port, out statusPort))
                    return;
            }
            if (ProcessStatusRequest(args, ip, statusPort))
                return;

            if (ProcessUnityBusyWindowLookup(args))
            {
                return;
            }


            try
            {
                ProcessStandard(args, ip, port);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message.Pastel(ConsoleColor.Red));
                return;
            }
        }
        finally
        {
#if DEBUG
     ///       Console.WriteLine("Debug mode. Waiting for <Enter> key...");
     //       Console.ReadLine();
#endif
        }
    }
}




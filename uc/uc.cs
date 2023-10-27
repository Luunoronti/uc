
using Newtonsoft.Json;
using Pastel;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Threading;

namespace uc
{
    internal class Program
    {
        private const int UDP_SERVER = 39143;
        private static string MY_LOC = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        static void Main(string[] args)
        {
            try
            {
                // check for command line
                // if we have /default then next param is an address
                // of default unity editor
                // else, if there is /H:_ then we have address of editor specified
                // else, we use default stored. if no default is stored,
                // then we show an error that says "set default or use /H:_

                #region TODO: Help
                if (args.Length == 0)
                {
                    #region General help
                    Console.WriteLine("Unity Command tool v. 0.2");
                    Console.WriteLine("Usage:");
                    Console.WriteLine("\tuc <command parameters> sends command to default host editor");
                    Console.WriteLine("\tuc [/H:host_address] <command parameters> sends command to specified host editor");
                    Console.WriteLine("\tuc [/defaulthost:host_address] sets the default host editor address");

                    Console.WriteLine("\tuc /disableOhMyPosh disables Oh-My-Posh information");
                    Console.WriteLine("\tuc /enableOhMyPosh enables Oh-My-Posh information");
                    Console.WriteLine();
                    #endregion
                    return;
                }
                #endregion

                #region Set default host if specified in cmd line (/default xx)
                try
                {
                    var hostToSetAsDefault = args.FirstOrDefault(a => a.IsDefaultHostArg())?.Substring("/defaulthost:".Length) ?? null;
                    if (string.IsNullOrEmpty(hostToSetAsDefault) == false)
                    {
                        File.WriteAllText(MY_LOC + "\\uc_default.cfg", hostToSetAsDefault);
                        Console.WriteLine($"Default host is set to {hostToSetAsDefault}.");
                        return;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message.Pastel(ConsoleColor.Red));
                    return;
                }
                #endregion

                #region Oh-my-posh functionality
                if (args.Any(a => a.IsDisableOhMyPoshArg()))
                {
                    File.WriteAllText(MY_LOC + "\\uc_ohmyposhdisabled.cfg", "disabled");
                    Console.WriteLine($"Oh-My-Posh integration is now disabled.");
                    return;
                }
                if (args.Any(a => a.IsEnableOhMyPoshArg()))
                {
                    File.WriteAllText(MY_LOC + "\\uc_ohmyposhdisabled.cfg", "enabled");
                    Console.WriteLine($"Oh-My-Posh integration is now enabled.");
                    return;
                }

                var oh_my_posh = args.Any(a => a.IsOhMyPoshArg());
                if (oh_my_posh)
                {
                    if (File.Exists(MY_LOC + "\\uc_ohmyposhdisabled.cfg"))
                    {
                        if (File.ReadAllText(MY_LOC + "\\uc_ohmyposhdisabled.cfg") == "disabled")
                        {
                            return; // it is disabled, show nothing
                        }
                    }
                }
                #endregion

                #region Select host (cmd line or default
                var defaultHost = File.Exists(MY_LOC + "\\uc_default.cfg") ? File.ReadAllText(MY_LOC + "\\uc_default.cfg") : null;
                var hostFromCmdLine = args.SingleOrDefault(a => a.IsHostArg())?.Substring(3) ?? null;
                var host = hostFromCmdLine ?? defaultHost;
                if (string.IsNullOrEmpty(host))
                {
                    if (oh_my_posh)
                        return; // return nothing for oh-my-posh in case of error
                    Console.WriteLine("Error: No host specified. Use /H: to specify host, or use /default to set default host.");
                    return;
                }
                #endregion

                #region Connect to editor
                UdpClient udp;
                try
                {
                    udp = new UdpClient();
                    udp.Connect(new IPEndPoint(IPAddress.Parse(host), UDP_SERVER));
                }
                catch (Exception ex)
                {
                    if (oh_my_posh)
                        return; // return nothing for oh-my-posh in case of error

                    Console.WriteLine(ex.Message.Pastel(ConsoleColor.Red));
                    return;
                }
                #endregion

                #region Prepare and send command
                var command = string.Join(" ", args.Where(a => !a.IsAnySysArg()).Select(arg => arg.QuotationIfWhitespaces()));

                if (oh_my_posh)
                    command = "status -ohmyposh"; // maybe we can get something better later

                var cmdObject = new EC_CommandStart { Command = command };
                var cmdJson = JsonConvert.SerializeObject(cmdObject);
                var data = System.Text.Encoding.UTF8.GetBytes(cmdJson);
                udp.Send(data, data.Length);
                #endregion

                #region Process messages
                while (true)
                {
                    try
                    {
                        if (udp.Available <= 0)
                        {
                            Thread.Sleep(1);
                            continue;
                        }

                        var e = new IPEndPoint(IPAddress.Parse(host), UDP_SERVER);

                        var rcvBuff = udp.Receive(ref e);
                        if (rcvBuff == null || rcvBuff.Length == 0)
                        {
                            if (oh_my_posh)
                                return; // return nothing for oh-my-posh in case of error
                            Console.WriteLine("Wrong message: Buffer is empty.".Pastel(ConsoleColor.Red));
                            return;
                        }

                        var msg = System.Text.Encoding.UTF8.GetString(rcvBuff, 0, rcvBuff.Length);
                        if (msg.Length == 0)
                        {
                            if (oh_my_posh)
                                return; // return nothing for oh-my-posh in case of error
                            Console.WriteLine("Wrong message: Message object representation is empty.".Pastel(ConsoleColor.Red));
                            return;
                        }

                        var bs = JsonConvert.DeserializeObject<EC_Base>(msg);
                        if (bs == null)
                        {
                            if (oh_my_posh)
                                return; // return nothing for oh-my-posh in case of error
                            Console.WriteLine("Wrong message: Message object is not valid EC_Base.".Pastel(ConsoleColor.Red));
                            return;
                        }

                        if (bs.Type == EC_Type.CommandCompleted)
                        {
                            // we may need this message later, for whatever reason
                            _ = JsonConvert.DeserializeObject<EC_CommandCompleted>(msg);
                            return;
                        }
                        else if (bs.Type == EC_Type.Message)
                        {
                            var m = JsonConvert.DeserializeObject<EC_Message>(msg);
                            Console.Write(m.Message);
                        }
                        else
                        {
                            if (oh_my_posh)
                                return; // return nothing for oh-my-posh in case of error
                            Console.WriteLine("Wrong message: Unknown message type.".Pastel(ConsoleColor.Red));
                            return;
                        }
                    }
                    catch (Exception ex)
                    {
                        if (oh_my_posh)
                            return; // return nothing for oh-my-posh in case of error
                        Console.WriteLine(ex.Message.Pastel(ConsoleColor.Red));
                        return;
                    }
                }
                #endregion
            }
            finally
            {
#if DEBUG
                Console.WriteLine("Debug mode. Waiting for <Enter> key...");
                Console.ReadLine();
#endif
            }
        }
    }

    public static class Extentions
    {
        public static bool IsHostArg(this string arg)
            => arg.ToLower().StartsWith("/h:");
        public static bool IsDefaultHostArg(this string arg)
            => arg.ToLower().StartsWith("/defaulthost:") && IPAddress.TryParse(arg.Substring("/defaulthost:".Length), out _);
        public static bool IsAnySysArg(this string arg)
            => IsHostArg(arg) || IsDefaultHostArg(arg) || IsOhMyPoshArg(arg)
            || IsDisableOhMyPoshArg(arg) || IsEnableOhMyPoshArg(arg);
        public static bool IsOhMyPoshArg(this string arg)
           => arg.ToLower() == "/ohmyposh";
        public static bool IsDisableOhMyPoshArg(this string arg)
           => arg.ToLower() == "/disableohmyposh";

        public static bool IsEnableOhMyPoshArg(this string arg)
           => arg.ToLower() == "/enableohmyposh";

        public static string QuotationIfWhitespaces(this string arg)
            => arg.Any(c => char.IsWhiteSpace(c)) ? $"\"{arg}\"" : arg;// IsHostArg(arg);
    }

    public class EC_CommandStart { public string Command { get; set; } }
    public enum EC_Type { CommandCompleted = 0, Message = 1 }

    public class EC_Base { public EC_Type Type { get; set; } }
    public class EC_CommandCompleted : EC_Base { }
    public class EC_Message : EC_Base { public string Message { get; set; } }

}

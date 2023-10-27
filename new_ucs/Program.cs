using Newtonsoft.Json;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;


internal class Program
{
    private static int UDP_SERVER = 39143;
    private static string UDP_IP = "";
    private static string MY_LOC = "C:\\Tools";

    static void Main(string[] args)
    {
        try
        {
            // 
            var cfgFile = Environment.CurrentDirectory + "\\editorconsole.json";
            if (!File.Exists(cfgFile))
            {
                return;
            }

            var cfg = JsonConvert.DeserializeObject<EditorConsoleConfig>(File.ReadAllText(cfgFile));
            UDP_SERVER = cfg?.statusport ?? -1;
            UDP_IP = cfg?.ip ?? "";

            if (UDP_SERVER == -1)
            {
                Console.Write("[stp err]");
                return;
            }
            if (string.IsNullOrEmpty(UDP_IP))
            {
                Console.Write("[ip err]");
                return;
            }

            UdpClient udp = new UdpClient();
            udp.Connect(new IPEndPoint(IPAddress.Parse(UDP_IP), UDP_SERVER));

            // send anything
            var d1 = new byte[1];
            udp.Send(d1, d1.Length);

            // now wait for responce, but not for longer than half one second
            var sw = Stopwatch.StartNew();
            while (true)
            {
                if (udp.Available > 0)
                {
                    IPEndPoint ep = default;
                    var rd = udp.Receive(ref ep);
                    if (rd == null || rd.Length == 0)
                    {
                        Console.Write("[eb err]");
                        return;
                    }
                    var msg = System.Text.Encoding.UTF8.GetString(rd, 0, rd.Length);
                    if (msg.Length == 0)
                    {
                        Console.Write("[em err]");
                        return;
                    }

                    // success
                    Console.Write(msg);
                    return;
                }
                sw.Stop();
                if (sw.Elapsed.TotalSeconds > 1)
                {
                    Console.Write("[timeout]");
                    return;
                }
                sw.Start();

                Thread.Sleep(1);
            }
        }
        catch(Exception ex)
        {
            Console.Write($"[ex err {ex.Message}]");
        }
       
    }
}


public class EditorConsoleConfig
{
    public string ip { get; set; }
    public int port { get; set; }
    public int statusport { get; set; }
}
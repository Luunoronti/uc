using System;
using System.Text;

namespace uc_net
{
    internal partial class Program
    {
        private static bool progressBegan = false;
        private static string progress_LastTitle = "";
        private static string progress_LastMsg = "";

        private static void HideProgress()
        {
            if (!progressBegan) return;
            progressBegan = false;

            var x = Console.CursorLeft;
            var y = Console.CursorTop;

            Console.WriteLine("".PadLeft(42 + Console.WindowWidth));
            Console.WriteLine("".PadLeft(42 + Console.WindowWidth));

            Console.CursorTop = y;
            Console.CursorLeft = x;

            //// we know we are at proper position,
            //// but we must clear everything
            //// and then, go back

            //var x = Console.CursorLeft;
            //var y = Console.CursorTop;

            //// we must clear everything
            ////Console.CursorLeft = 0;
            ////Console.CursorTop = progress_ConY1;
            //Console.Write("".PadLeft(Console.WindowWidth));

            ////Console.CursorLeft = 0;
            ////Console.CursorTop = progress_ConY2;
            //Console.Write("".PadLeft(Console.WindowWidth));

            //Console.CursorLeft = x;
            //Console.CursorTop = y;
            Console.CursorVisible = true;
        }



        private static void ShowProgress(string title, string message, int progress, int cpu, int ram)
        {    
            // store current pos
            var x = Console.CursorLeft;
            var y = Console.CursorTop;

            if (progressBegan == false)
            {
                Console.Write("\n\n\n\n");
                Console.CursorTop = Console.CursorTop - 4;
                Console.CursorLeft = x;
                y = Console.CursorTop;
                
                progressBegan = true;
                Console.CursorVisible = false;
            }

            // now, prepare string
            var sb = new StringBuilder();
            sb.Append("\n");

            var ttl = title;
            var msg = message;
            if (title.Length < progress_LastTitle.Length) ttl = title.PadRight(progress_LastTitle.Length);
            if (message.Length < progress_LastMsg.Length) msg = message.PadRight(progress_LastMsg.Length);

            progress_LastTitle = title;
            progress_LastMsg = message;

            var cpuFlag = cpu > 40 ? (cpu > 80 ? "\u001b[31m" : "\u001b[33m") : "\u001b[32m";
            var ramFlag = ram > 40 ? (ram > 80 ? "\u001b[31m" : "\u001b[33m") : "\u001b[32m";

            var str = $"CPU:{cpuFlag}{cpu,3}%\u001b[0m  RAM:{ramFlag}{ram,3}%\u001b[0m";

            sb.Append($"\u001b[33m[\u001b[32m{"".PadRight(progress / 5, '█').PadRight(20, '-')}\u001b[33m]\u001b[32m {progress,3}%\u001b[0m");
            sb.Append($" \u001b[1m\u001b[93m{ttl}\u001b[0m");
            sb.Append("\n");
            sb.Append($"{str,-42}{msg}");

            Console.Write(sb.ToString());
            Console.CursorTop = y;
            Console.CursorLeft = x;
        }
    }
}
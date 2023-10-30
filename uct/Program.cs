using System;

namespace uct
{
    internal class Program
    {
        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("Please specify title in arguments.");
                return;
            }

            var title = string.Join(" ", args);
            Console.Title = title;
            Console.WriteLine($"Title set to {title}");
        }
    }
}

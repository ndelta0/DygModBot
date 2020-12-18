using System;
using System.Threading.Tasks;

namespace DygBot
{
    public static class Program
    {
        public static Task Main()
        {
            Console.CancelKeyPress += delegate(object _, ConsoleCancelEventArgs args)
            {
                args.Cancel = true;
                Startup.KeepRunning = false;
            };
            return Startup.RunAsync();
        }
    }
}

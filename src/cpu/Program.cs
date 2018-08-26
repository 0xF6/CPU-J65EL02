namespace vm
{
    using System;
    using System.Threading;
    using RC.Framework.Screens;

    class Program
    {
        static void Main(string[] args)
        {
            Console.Title = "cpu_host";
            RCL.EnablingVirtualTerminalProcessing();
            RCL.SetThrowCustomColor(false);
            new Thread((() =>
            {
                new Machine(@"C:\Git\CPU-J65EL02\src\cpu\bootloader.bin", 0x2000).run();
            })).Start();
            Console.ReadLine();
        }
    }
}

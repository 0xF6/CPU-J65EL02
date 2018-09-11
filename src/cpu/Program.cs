namespace vm
{
    using System;
    using System.Threading;
    using RC.Framework.Screens;

    class Program
    {
        private static Machine machine;
        static void Main(string[] args)
        {
            Console.Title = "cpu_host";
            RCL.EnablingVirtualTerminalProcessing();
            RCL.SetThrowCustomColor(false);


            machine = new Machine(@".\bootloader.efl", null, 0x20000);
            machine.run();
            machine = null;
            Console.ReadLine();
        }
    }
}

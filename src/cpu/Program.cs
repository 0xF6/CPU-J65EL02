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
            

            //machine = new Machine(@"C:\Git\CPU-J65EL02\src\bootloader\bootloader.efl", null /*@".\redforth.img"*/, 0x20000);
            machine = new Machine(@"ehbasic.rom", null /*@".\redforth.img"*/, 0x20000);
            machine.run();
            Console.ReadLine();
        }
    }
}

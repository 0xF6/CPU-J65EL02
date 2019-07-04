namespace vm
{
    using System;
    using System.Linq;
    using RC.Framework.Screens;

    class Program
    {
        private static Machine machine;
        static void Main(string[] args)
        {
            Console.Title = "cpu_host";
            RCL.EnablingVirtualTerminalProcessing();
            RCL.SetThrowCustomColor(false);
            machine = new Machine(args.First(), null /*@".\redforth.img"*/, 0x20000);
            machine.run();
            Console.ReadLine();
        }
    }
}

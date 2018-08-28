namespace vm
{
    using System;
    using System.Text;
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
                new Machine(@".\..\..\..\hello.bin", @".\..\..\..\hello_world.bin" , 0x20000).run();
            })).Start();
            Console.ReadLine();
        }
    }
}

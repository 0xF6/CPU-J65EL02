namespace vm
{
    using System;
    using System.Linq;
    using System.Runtime.InteropServices;

    internal class Host
    {
        private static Machine machine;
        public static void Main(string[] args)
        {
            if(RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) Console.Title = "cpu_host";
            machine = new Machine(args.First(), null /*@".\redforth.img"*/, 0x20000);
            machine.run();
            Console.ReadLine();
        }
    }
}

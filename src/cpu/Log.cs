namespace vm
{
    using System;
    using System.Drawing;
    using RC.Framework.Screens;

    public static class Log
    {
        public static void nf(object cnt, string section = "BIOS")
        {
            if (section == "BIOS") section = RCL.Wrap("BIOS", Color.GreenYellow);
            Screen.WriteLine($"{section} ->> {cnt}");
        }
        public static void wr(object cnt, string section = "BIOS")
        {
            if (section == "BIOS") section = RCL.Wrap("BIOS", Color.GreenYellow);
            Screen.WriteLine($"{section} ->> {cnt}");
        }
        public static void ft(object cnt, string section = "BIOS")
        {
            if (section == "BIOS") section = RCL.Wrap("BIOS", Color.GreenYellow);
            Screen.WriteLine($"{section} ->> {cnt.ToString().To(Color.Red)}");
        }
        public static void er(object cnt, string section = "BIOS")
        {
            if (section == "BIOS") section = RCL.Wrap("BIOS", Color.GreenYellow);
            Screen.WriteLine($"{section} ->> {cnt}");
        }
    }
}
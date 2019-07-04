namespace vm
{
    using System.Drawing;
    using Pastel;
    using static System.Console;

    public static class Log
    {
        public static void nf(object cnt, string section = "BIOS")
        {
            if (section == "BIOS") section = "BIOS".Pastel(Color.GreenYellow);
            WriteLine($"{section} ->> {cnt}");
        }
        public static void wr(object cnt, string section = "BIOS")
        {
            if (section == "BIOS") section = "BIOS".Pastel(Color.GreenYellow);
            WriteLine($"{section} ->> {cnt}");
        }
        public static void ft(object cnt, string section = "BIOS")
        {
            if (section == "BIOS") section = "BIOS".Pastel(Color.GreenYellow);
            WriteLine($"{section} ->> {cnt.ToString().Pastel(Color.Red)}");
        }
        public static void er(object cnt, string section = "BIOS")
        {
            if (section == "BIOS") section = "BIOS".Pastel(Color.GreenYellow);
            WriteLine($"{section} ->> {cnt}");
        }
    }
}
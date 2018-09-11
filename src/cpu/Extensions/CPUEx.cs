namespace vm.extensions
{
    using System;
    using System.Linq;
    using components;
    using cpu;
    using exceptions;

    public static class CPUEx
    {
        public static string getProcessorStatusString(this CPU cpu)
        {
            return "[" + (cpu.negativeFlag ? 'N' : '*') + "-" +
                   (cpu.state.overflowFlag ? 'V' : '*') + "-" +
                   (cpu.state.breakFlag ? 'B' : '*') + "-" +
                   (cpu.state.decimalModeFlag ? 'D' : '*') + "-" +
                   (cpu.state.irqDisableFlag ? 'I' : '*') + "-" +
                   (cpu.zeroFlag ? 'Z' : '*') + "-" +
                   (cpu.carryFlag ? 'C' : '*') +
                   "]";
        }

        public static void Hault(this CPU cpu, Exception e)
        {
            cpu.state.signalStop = true;
            if (e != null)
            {
                Console.BackgroundColor = ConsoleColor.White;
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Clear();
                Console.CursorVisible = false;

                Console.WriteLine("\n");
                Console.WriteLine("\t========  ========  ========  ========  |");
                Console.WriteLine("\t=      =  =      =  =      =  =         |");
                Console.WriteLine("\t=      =  =      =  =      =  =         |");
                Console.WriteLine("\t=      =  =      =  ========  ========  |");
                Console.WriteLine("\t=      =  =      =  =                =  |");
                Console.WriteLine("\t=      =  =      =  =                =   ");
                Console.WriteLine("\t========  ========  =         ========  *");
                Console.WriteLine("\n");

                var msg = e.GetType()
                    .Name
                    .Select(x => char.IsUpper(x) ? $" {x}" : x.ToString())
                    .ToArray();

                var msg2 = string.Join("", msg).ToLower();
                Console.WriteLine($"\t{msg2} at $0x{cpu.state.lastMemory:X8}");
                Console.WriteLine($"\t\t  {e.Message}");
                if (e is BiosException v)
                    Console.WriteLine($"\t\t{v.Description}");
                else
                    Console.WriteLine("\n");
                Console.WriteLine("\tCPUState:");
                Console.WriteLine($"\t\tA: 0x{cpu.state.A:X4}; AT: 0x{cpu.state.A_TOP:X4}; R: 0x{cpu.state.R:X4};");
                Console.WriteLine($"\t\tD: 0x{cpu.state.D:X4}; IR: 0x{cpu.state.IR:X4}; X: 0x{cpu.state.X:X4};");
                Console.WriteLine($"\t\tS: 0x{cpu.state.SP:X4}; PC: 0x{cpu.state.PC:X4}; Y: 0x{cpu.state.Y:X4};");
                var flags = cpu.getProcessorStatusString();
                Console.WriteLine($"\t\t{flags}");
                Console.WriteLine("\tDevices:");
                Console.WriteLine($"\t\t{string.Join(", \n\t\t", cpu.Bus.devices.Select(x => x.ToString()).ToArray())}");
                Console.WriteLine("\n");
                Console.WriteLine("\t\t\t\tPress the 'RESET' button on restart controller.");

            }
            else
            {
                Log.ft("HAULT CPU");
            }
        }
    }
}
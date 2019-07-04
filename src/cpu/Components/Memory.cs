namespace vm.components
{
    using System;
    using System.Drawing;
    using System.IO;
    using System.Threading;
    using cpu;
    using devices;
    using exceptions;
    using Pastel;

    public class Memory : Device
    {
        private readonly byte[] mem;

        public Memory(int startAddress, int endAddress, CPU cp) : base(startAddress, endAddress, cp)
        {
            if (endAddress >= 0x40000)
                throw new OverFlowHeapMemoryException("!!address >= 0x40000!!");
            this.mem = new byte[Size];
            ou($"alloc {mem.Length} bytes - 0x{startAddress:X4}->0x{(endAddress + 1):X4}");
        }
        


        #region static

        public static int address(int lowByte, int hiByte) => ((hiByte << 8) | lowByte) & 0xffff;

        #endregion

        public override void write(int address, int data)
        {
            if (address >= this.mem.Length) return;
            this.mem[address] = (byte)(data & 0xff);
            os($"%write<-addr:{address:X4}<<-0x{data:X4};");
            cpu.state.lastMemory = address;
        }

        public override int read(int address, bool cpuAccess)
        {
            if (address >= this.mem.Length) return 0;
            var result = this.mem[address] & 0xff;
            os($"%read->addr->{address:X4};cpu:{cpuAccess}->>0x{result:X4};");
            cpu.state.lastMemory = address;
            return result;
        }
        public void loadFromFile(string file, int memOffset, int maxLen, string type)
        {
            using (var stream = new MemoryStream(File.ReadAllBytes(file)))
            {
                var offset = memOffset;
                var read = stream.Read(this.mem, memOffset, maxLen);
                ou($"%load->{Path.GetFileName(file)} as {type}->{offset},{read}");
            }
        }

        public void clear() => this.mem.Fill((byte)0);
        private void ou(object s) => Log.nf(s, "MEM".Pastel(Color.Red));
        private void os(object s)
        {
            s_deb_mem = s.ToString();
            if(is_deb_mem_st)
                return;
            new Thread((() =>
            {
                while (true)
                {
                    backcycle = cpu.state.stepCounter;
                    Thread.Sleep(200);
                    newcycle = cpu.state.stepCounter;
                    var cc = (newcycle - backcycle) * 5;
                    Console.Title = $"cpu_host | {s_deb_mem} | cycle: {cc}/s";
                }
            })).Start();
            is_deb_mem_st = true;
        }

        private string s_deb_mem;
        private bool is_deb_mem_st;
        private long backcycle, newcycle;
    }

    public static class ArrEx
    {
        public static void Fill<T>(this T[] array, T value)
        {
            for (var i = 0; i < array.Length; i++)
            {
                array[i] = value;
            }
        }
    }
}
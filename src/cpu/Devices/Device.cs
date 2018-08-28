namespace vm.devices
{
    using System;
    using cpu;

    /// <summary>
    /// A memory-mapped IO Device.
    /// </summary>
    public abstract class Device : IComparable
    {
        protected readonly CPU cpu;
        public int startAddress { get; protected set; }
        public int endAddress { get; protected set; }

        public int Size => this.endAddress - this.startAddress + 1;
        public int StartAddress => this.startAddress;

        protected Device(int startAddress, int endAddress, CPU cp)
        {
            cpu = cp;
            this.startAddress = startAddress;
            this.endAddress = endAddress;
        }

        public virtual string getName() => "<???>";

        public bool inRange(int address) 
            => address >= this.startAddress && address <= this.endAddress;

        public abstract void write(int address, int data);
        public abstract int read(int address, bool cpuAccess);

        public override int GetHashCode() => 
            startAddress ^ 43 + 
            endAddress ^ 43;

        public int CompareTo(object obj)
        {
            if (obj is Device dev)
            {
                return string.Compare(getName(), dev.getName(), StringComparison.Ordinal);
            }
            return -1;
        }

        public override string ToString() => $"{getName()} @0x{startAddress:X5}-0x{endAddress:X5}";
    }
}
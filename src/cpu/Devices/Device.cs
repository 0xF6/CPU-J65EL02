namespace cpu.Devices
{
    /// <summary>
    /// A memory-mapped IO Device.
    /// </summary>
    public abstract class Device
    {
        protected int startAddress { get; set; }
        protected int endAddress { get; set; }

        public int Size => this.endAddress - this.startAddress + 1;
        public int StartAddress => this.startAddress;

        protected Device(int startAddress, int endAddress)
        {
            this.startAddress = startAddress;
            this.endAddress = endAddress;
        }

        public bool inRange(int address) 
            => address >= this.startAddress && address <= this.endAddress;

        public abstract void write(int address, int data);
        public abstract int read(int address, bool cpuAccess);

        public override int GetHashCode() => 
            startAddress ^ 43 + 
            endAddress ^ 43;
    }
}
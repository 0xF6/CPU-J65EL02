namespace vm.devices
{
    using cpu;

    public class RedBus : Device 
    {
        /// <summary>
        /// A Redbus peripheral.
        /// </summary>
        public interface Peripheral
        {
            void write(int address, int data);
            int read(int address);
            void update();
        }

        public RedBus(CPU cpu) : base(-1, -1, cpu) { }  // there is no fixed address for the redbus

        private readonly Peripheral[] peripherals = new Peripheral[0x100];

        public int activeDeviceID  { get; set; }
        private bool enabled { get; set; }

        public bool enableWindow { get; set; }
        public override void write(int address, int data)
        {
            if (!this.enabled) return;
            this.peripherals[this.activeDeviceID]?.write(address, data & 0xff);
        }

        public override int read(int address, bool cpuAccess)
        {
            if (!this.enabled) return 0;
            return this.peripherals[this.activeDeviceID]?.read(address) ?? 0;
        }
        public int WindowsOffset
        {
            get => this.startAddress;
            set
            {
                this.startAddress = value;
                this.endAddress = value + 0xff;
            }
        }
        public int MemoryWindow { get; set; }
        public void Enable() => this.enabled = true;
        public void Disable() => this.enabled = false;
        public void setPeripheral(int id, Peripheral peripheral) => this.peripherals[id] = peripheral;
        public void updatePeripheral() => this.peripherals[this.activeDeviceID]?.update();
        
    }
}
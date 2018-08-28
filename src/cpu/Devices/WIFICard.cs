namespace vm.devices
{
    using cpu;

    public class WIFICard : Device
    {
        public WIFICard(int startAddress, CPU cp) : base(startAddress, startAddress + 0x100, cp)
        {
        }

        public override string getName() => "intel wifi ac8265";

        public override void write(int address, int data)
        {
            throw new System.NotImplementedException();
        }

        public override int read(int address, bool cpuAccess)
        {
            throw new System.NotImplementedException();
        }
    }
}
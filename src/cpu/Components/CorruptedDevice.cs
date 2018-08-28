namespace vm.components
{
    using cpu;
    using devices;
    using exceptions;

    public class CorruptedDevice : Device
    {
        public CorruptedDevice() : base(-1, -1, null)
        {
        }

        public override void write(int address, int data)
        {
            throw new CorruptedMemoryException("Memory could not be WRITE.", this);
        }

        public override int read(int address, bool cpuAccess)
        {
            throw new CorruptedMemoryException("Memory could not be READ.", this);
        }
    }
}
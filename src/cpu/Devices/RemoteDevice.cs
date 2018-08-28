namespace vm.devices
{
    using cpu;

    public abstract class RemoteDevice : Device
    {
        protected RemoteDevice(int startAddress, int endAddress, CPU cp) : base(startAddress, endAddress, cp)
        {
        }

        public abstract string getPhysicalAddress();
        public abstract int getPhysicalPort();
    }
}
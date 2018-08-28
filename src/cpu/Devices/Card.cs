namespace vm.devices
{
    public abstract class Card : RedBus.Peripheral
    {
        public abstract void write(int address, int data);
        public abstract int read(int address);
        public abstract void update();

        public virtual string PhysicalAddress() => "00:00:00:00:00";
    }
}
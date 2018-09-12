namespace vm.components
{
    using cpu;
    using devices;

    public class Bios
    {
        private readonly Machine _machine;
        private readonly int _startPoint;
        private readonly string _registerInit;

        private RedBus redBus;
        private CPU cpu;
        private Memory ram;

        public Bios(Machine machine, int startPoint, string RegisterInit)
        {
            _machine = machine;
            _startPoint = startPoint;
            _registerInit = RegisterInit;
        }

        public CPU initCPU()
        {
            return cpu = new CPU(false);
        }

        public Bus initBus()
        {
            redBus = new RedBus(cpu);
            return new Bus(redBus);
        }

        public Memory initRAM() => ram = new Memory(read<int>("memory_start_address"), read<int>("memory_end_address"), cpu);

        public void loadBiosFirware(string path)
        { }

        public T read<T>(string key) => default;
    }
}
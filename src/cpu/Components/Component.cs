namespace vm.components
{
    using cpu;
    using devices;

    public class Component
    {
        private readonly CPU _cpu;

        public Component(CPU cpu) => _cpu = cpu;


        protected CPU getCPU() => _cpu;
        protected CpuState getState() => _cpu.state;
        protected Screen getScreen() => _cpu.screen;
        protected Bus getBus() => _cpu.Bus;
        protected Stack getStack() => _cpu.stack;
    }
}
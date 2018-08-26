namespace vm.devices
{
    using cpu;

    public class Screen
    {
        private readonly CPU _cpu;

        public Screen(CPU cpu) => _cpu = cpu;

        public int maskMWidth() => _cpu.state.mWidthFlag ? 0xff : 0xffff;
        public int maskXWidth() => _cpu.state.indexWidthFlag ? 0xff : 0xffff;
        public int negativeMWidth() => _cpu.state.mWidthFlag ? 0x80 : 0x8000;
        public int negativeXWidth() => _cpu.state.indexWidthFlag ? 0x80 : 0x8000;
    }
}
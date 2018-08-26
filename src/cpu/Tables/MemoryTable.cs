namespace vm.cpu.tables
{
    using components;

    public abstract class MemoryTable : MonoCore
    {
        protected int readMemory(int address, bool x)
        {
            if (state.emulationFlag || (x ? state.indexWidthFlag : state.mWidthFlag))
                return readByte(address);
            return readWord(address);
        }
        protected void writeMemory(int address, int value, bool x)
        {
            Bus.write(address, value);
            var flag = x ? state.indexWidthFlag : state.mWidthFlag;
            if (!state.emulationFlag && !flag)
                Bus.write(address + 1, value >> 8);
        }

        protected int readByte(int address) => Bus.read(address, true);
        protected int readWord(int address) => readByte(address) | (readByte(address + 1) << 8);
    }
}
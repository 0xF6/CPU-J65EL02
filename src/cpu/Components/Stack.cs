namespace vm.components
{
    using System.Drawing;
    using cpu;
    using cpu.tables;
    using Pastel;

    public class Stack : Component
    {
        public Stack(CPU cpu) : base(cpu) { }
        public void RPush(int data, bool x)
        {
            ou($"rpush 0x{data:X4},{x}");
            var flag = x ? getState().indexWidthFlag : getState().mWidthFlag;
            if (!getState().emulationFlag && !flag) RPushWord(data);
            else RPushByte(data);
        }

        public void RPushByte(int data)
        {
            ou($"rpush 0x{data:X4}");
            getState().R = (getState().R - 1) & 0xffff;
            getBus().write(getState().R, data);
        }

        public void RPushWord(int data)
        {
            ou($"rpush-w ^");
            RPushByte((data >> 8) & 0xff);
            RPushByte(data & 0xff);
            ou($"rpush-w ;");
        }

        public int RPop(bool x)
        {
            ou($"rpop {x}");
            var flag = x ? getState().indexWidthFlag : getState().mWidthFlag;
            if (!getState().emulationFlag && !flag)
                return RPopWord();
            return RPopByte();
        }

        public int RPopByte()
        {
            var val = getBus().read(getState().R, true);
            getState().R = (getState().R + 1) & 0xffff;
            ou($"rpop <<-0x{val:X4}");
            return val;
        }

        public int RPopWord() => RPopByte() | (RPopByte() << 8);

        public void Push(int data, bool x)
        {
            ou($"push 0x{data:X4},{x}");
            if (!getState().emulationFlag && !(x ? getState().indexWidthFlag : getState().mWidthFlag))
                PushWord(data);
            else
                PushByte(data);
        }
        public void Push(int data)
        {
            //ou($"push 0x{data:X4}");
            getBus().write(0x100 + getState().SP, data);

            if (getState().SP == 0)  getState().SP = 0xff;
            else --getState().SP;
        }

        /**
         * Push an item onto the stack, and decrement the stack counter.
         * Will wrap-around if already at the bottom of the stack (This
         * is the same behavior as the real 6502)
         */
        public void PushByte(int data)
        {
            if (!getCPU().StackBug)
                getBus().write(getState().SP, data);
            var bottom = getState().emulationFlag ? RegisterTable.S_STACK_TOP - 0x100 : 0;
            if (getState().SP <= bottom)
                getState().SP = RegisterTable.S_STACK_TOP;
            else --getState().SP;
            if (getCPU().StackBug)
                getBus().write(getState().SP, data);
        }

        public void PushWord(int data)
        {
            PushByte((data >> 8) & 0xff);
            PushByte(data & 0xff);
        }

        public int Pop(bool x = false)
        {
            ou($"pop {x}");
            var word = !getState().emulationFlag && !(x ? getState().indexWidthFlag : getState().mWidthFlag);
            return word ? PopWord() : PopByte();
        }

        /**
         * Pre-increment the stack pointer, and return the top of the stack. Will wrap-around if already
         * at the top of the stack (This is the same behavior as the real 6502)
         */
        public int PopByte()
        {
            var val = 0;
            if (getCPU().StackBug)
                val = getBus().read(getState().SP, true);
            if (getState().emulationFlag && getState().SP >= RegisterTable.S_STACK_TOP)
                getState().SP = RegisterTable.S_STACK_TOP - 0x100;
            else
                ++getState().SP;
            if (!getCPU().StackBug)
                val = getBus().read(getState().SP, true);
            ou($"popb <<-0x{val:X4}");
            return val;
        }

        public int PopWord() => PopByte() | (PopByte() << 8);
        private void ou(object s) => Log.nf(s, "STACK".Pastel(Color.DeepPink));
    }
}
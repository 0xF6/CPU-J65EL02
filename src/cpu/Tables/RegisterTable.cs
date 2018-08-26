namespace vm.cpu.tables
{
    public abstract class RegisterTable : InstructionTable
    {
        /* Process status register mnemonics */
        public static readonly int P_CARRY = 0x01;
        public static readonly int P_ZERO = 0x02;
        public static readonly int P_IRQ_DISABLE = 0x04;
        public static readonly int P_DECIMAL = 0x08;
        public static readonly int P_BREAK_OR_X = 0x10;
        public static readonly int P_MFLAG = 0x20;
        public static readonly int P_OVERFLOW = 0x40;
        public static readonly int P_NEGATIVE = 0x80;

        public static readonly int S_STACK_TOP = 0x200;
        public static readonly int R_STACK_TOP = 0x300;

        // NMI vector
        public static readonly int NMI_VECTOR_L = 0xfffa;
        public static readonly int NMI_VECTOR_H = 0xfffb;
        // Reset vector
        public static readonly int RST_VECTOR_L = 0xfffc;
        public static readonly int RST_VECTOR_H = 0xfffd;
        // IRQ vector
        public static readonly int IRQ_VECTOR_L = 0xfffe;
        public static readonly int IRQ_VECTOR_H = 0xffff;
    }
}
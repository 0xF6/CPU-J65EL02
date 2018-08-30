namespace vm.cpu
{
    using System.Text;
    using tables;

    public class CpuState
    {
        /// <summary>
        /// Accumulator
        /// </summary>
        public int A { get; set; }
        /// <summary>
        /// logical register for hi bits of A
        /// </summary>
        public int A_TOP { get; set; }
        /// <summary>
        /// X index regsiter
        /// </summary>
        public int X { get; set; }
        /// <summary>
        /// Y index register
        /// </summary>
        public int Y { get; set; }
        /// <summary>
        /// Stack Pointer
        /// </summary>
        public int SP { get; set; }
        /// <summary>
        /// Program Counter
        /// </summary>
        public int PC { get => pcc;
            set
            {
               
                pcc = value;
            }
        }

        private int pcc;
        /// <summary>
        /// Last Loaded Instruction Register
        /// </summary>
        public int IR { get; set; }
        /// <summary>
        /// 65el02 I register
        /// </summary>
        public int I { get; set; }
        /// <summary>
        /// 65el02 R stack pointer
        /// </summary>
        public int R { get; set; }
        /// <summary>
        /// 65el02 D register
        /// </summary>
        public int D { get; set; }
        /// <summary>
        /// 65el02 BRK address
        /// </summary>
        public int BRK { get; set; }
        /// <summary>
        /// 65el02 POR address
        /// </summary>
        public int POR { get; set; } = 0x400;

        /**
         * Peek-Ahead to next IR
         */
        public int nextIr;
        public int[] args = new int[2];
        public int[] nextArgs = new int[2];
        public int instSize;
        public bool opTrap;
        public bool irqAsserted;
        public bool nmiAsserted;
        public int lastPc;
        public bool intWait;
        public bool signalStop;

        /* Status Flag Register bits */
        public bool carryFlag;
        public bool negativeFlag;
        public bool zeroFlag;
        public bool irqDisableFlag;
        public bool decimalModeFlag;
        public bool breakFlag;
        public bool overflowFlag;
        public bool emulationFlag = true;
        public bool mWidthFlag = true;
        public bool indexWidthFlag = true;
        public long stepCounter = 0L;
        public long lastMemory;


        public CpuState() { }

        public int getInstructionSize(int insn)
        {
            var m = mWidthFlag ? 1 : 0;
            var x = indexWidthFlag ? 1 : 0;
            switch (insn)
            {
                case 0x69: // ADC IMM
                    return 3 - m;
                case 0xe9: // SBC IMM
                    return 3 - m;
                case 0xc9: // CMP IMM
                    return 3 - m;
                case 0xe0: // CPX IMM
                    return 3 - x;
                case 0xc0: // CPY IMM
                    return 3 - x;
                case 0x29: // AND IMM
                    return 3 - m;
                case 0x49: // EOR IMM
                    return 3 - m;
                case 0x09: // ORA IMM
                    return 3 - m;
                case 0x89: // BIT IMM
                    return 3 - m;
                case 0xa9: // LDA IMM
                    return 3 - m;
                case 0xa2: // LDX IMM
                    return 3 - x;
                case 0xa0: // LDY IMM
                    return 3 - x;
                default:
                    return InstructionTable.instructionSizes[insn];
            }
        }
        public string getInstructionByteStatus()
        {
            switch (getInstructionSize(IR))
            {
                case 0:
                case 1:
                    return $"{lastPc:X4} {IR:X2}        ";
                case 2:
                    return $"{lastPc:X4} {IR:X2} {args[0]:X2}     ";
                case 3:
                    return $"{lastPc:X4} {IR:X2} {args[0]:X2} {args[1]:X2}  ";
                default:
                    return "???";
            }
        }
        public string ToTraceEvent()
        {
            var opcode = CPU.disassembleOp(IR, args, getInstructionSize(IR));
            var a1 = $"{getInstructionByteStatus()}";
            a1 +=$" {IR:X} {opcode.Substring(0, 3).ToUpper()} ";
            a1 +=$"A:0x{A:X2} B:0x{(A_TOP >> 8):X} X:0x{X:X} Y:0x{Y:X} I:0x{I:X} D:0x{D:X} ";
            a1 +=$"F:0x{getStatusFlag():X} ";
            a1 +=$"S:0x{SP:X} R:0x{R:X}";
            return a1;
        }
        public int getStatusFlag()
        {
            var status = 0;
            if (carryFlag)
                status |= RegisterTable.P_CARRY;
            if (zeroFlag)
                status |= RegisterTable.P_ZERO;
            if (irqDisableFlag)
                status |= RegisterTable.P_IRQ_DISABLE;
            if (decimalModeFlag)
                status |= RegisterTable.P_DECIMAL;
            if (emulationFlag && breakFlag)
                status |= RegisterTable.P_BREAK_OR_X;
            else if (indexWidthFlag)
                status |= RegisterTable.P_BREAK_OR_X;
            if (emulationFlag)
                status |= 0x20;
            else if (mWidthFlag)
                status |= RegisterTable.P_MFLAG;
            if (overflowFlag)
                status |= RegisterTable.P_OVERFLOW;
            if (negativeFlag)
                status |= RegisterTable.P_NEGATIVE;
            return status;
        }
        public override string ToString() => ToTraceEvent();
    }
}
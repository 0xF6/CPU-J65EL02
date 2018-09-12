namespace vm.cpu
{
    using components;
    using extensions;
    using System;
    using System.Text;
    using System.Threading;
    using tables;
    using Screen = devices.Screen;

    public partial class CPU : MemoryTable, IDisposable
    {
        public SpinWait CPUSpin = new SpinWait();
        public Screen screen { get; set; }
        public Instructor instructor { get; set; }
        public Stack stack { get; set; }
        public Debugger debugger { get; set; }
        /// <summary>
        /// true - 6502
        /// false - 65C02
        /// </summary>
        public bool IsLight { get; private set; }

        #region Flags

        public bool carryFlag
        {
            get => state.carryFlag;
            set => state.carryFlag = value;
        }
        public bool zeroFlag
        {
            get => state.zeroFlag;
        }
        public bool IrqDisableFlag
        {
            get => state.irqDisableFlag;
            set => state.irqDisableFlag = value;
        }
        public bool DecimalModeFlag
        {
            get => state.decimalModeFlag;
            set => state.decimalModeFlag = value;
        }
        public bool overFlowFlag
        {
            get => state.overflowFlag;
            set => state.overflowFlag = value;
        }
        public bool negativeFlag
        {
            get => state.negativeFlag;
            set => state.negativeFlag = value;
        }
        public bool BreakFlag
        {
            get => state.breakFlag;
            set => state.breakFlag = value;
        }
        public bool IRD
        {
            get => state.irqAsserted;
            set => state.irqAsserted = value;
        }
        public bool NMI
        {
            get => state.nmiAsserted;
            set => state.nmiAsserted = value;
        }
        public bool opTrap
        {
            get => state.opTrap;
            set => state.opTrap = value;
        }
        public int CarryBit => state.carryFlag ? 1 : 0;
        #endregion



        public int ProcessorStatus
        {
            get => 0;
            set
            {
                carryFlag = (value & P_CARRY) != 0;
                instructor.setZeroFlag((value & P_ZERO) != 0); ;
                IrqDisableFlag = (value & P_IRQ_DISABLE) != 0;
                DecimalModeFlag = (value & P_DECIMAL) != 0;


                if (state.emulationFlag)
                {
                    state.indexWidthFlag = true;
                    state.breakFlag = (value & P_BREAK_OR_X) != 0;
                }
                else
                {
                    if ((value & P_BREAK_OR_X) != 0)
                    {
                        state.indexWidthFlag = true;
                        state.X &= 0xff;
                        state.Y &= 0xff;
                    }
                    else state.indexWidthFlag = false;
                }

                if (!state.emulationFlag)
                {
                    var was8Bit = state.mWidthFlag;
                    if ((value & P_MFLAG) != 0)
                    {
                        state.mWidthFlag = true;
                        if (!was8Bit)
                        {
                            state.A_TOP = state.A & 0xff00;
                            state.A &= 0xff;
                        }
                    }
                    else
                    {
                        state.mWidthFlag = false;
                        if (was8Bit)
                            state.A = state.A_TOP | (state.A & 0xff);
                    }
                }
                else state.mWidthFlag = true;

                overFlowFlag = (value & P_OVERFLOW) != 0;
                negativeFlag = (value & P_NEGATIVE) != 0;
            }
        }
        /// <summary>
        /// Given a single byte, compute the offset address.
        /// </summary>
        private int relAddress(int offset)
        {
            // Cast the offset to a signed byte to handle negative offsets
            return (state.PC + (byte)offset) & 0xffff;
        }

        public void setProgramCounter(int addr)
        {
            state.PC = addr;

            // As a side-effect of setting the program counter,
            // we want to peek ahead at the next state.
            peekAhead();
        }
        public CPU(bool light)
        {
            IsLight = light;
            screen = new Screen(this);
            instructor = new Instructor(this);
            stack = new Stack(this);
        }

        private long opBeginTime;
        private void delayLoop(int opcode)
        {
            var clockSteps = instructionClocksNmos[0xff & opcode];

            if (clockSteps == 0)
            {
                Log.wr($"Opcode {opcode:X2} has clock step of 0!");
                return;
            }

            var interval = clockSteps * CLOCK_SPEED["2MHz"];

            do
            {
                CPUSpin.SpinOnce();
            }
            while (opBeginTime + interval >= DateTime.Now.Ticks);

        }

        private void cycleAddress(ref int effectiveAddress, ref int irAddressMode)
        {
            switch (irAddressMode)
            {
                case 0x0: // #Immediate
                    break;
                case 0x1: // Zero Page
                    effectiveAddress = state.args[0];
                    break;
                case 0x2: // Accumulator - ignored
                    break;
                case 0x3: // Absolute
                    effectiveAddress = Memory.address(state.args[0], state.args[1]);
                    break;
                case 0x4: // 65C02 (Zero Page)
                    if (IsLight)
                        effectiveAddress = Memory.address(Bus.read(state.args[0], true),
                                                             Bus.read((state.args[0] + 1) & 0xff, true));
                    break;
                case 0x5: // Zero Page,X / Zero Page,Y
                    if (state.IR == 0x14)
                        effectiveAddress = state.args[0]; // 65C02 TRB Zero Page
                    else if (state.IR == 0x96 || state.IR == 0xb6)
                        effectiveAddress = zpyAddress(state.args[0]);
                    else
                        effectiveAddress = zpxAddress(state.args[0]);
                    break;
                case 0x7:
                    if (state.IR == 0x9c || state.IR == 0x1c) // 65C02 STZ & TRB Absolute
                        effectiveAddress = Memory.address(state.args[0], state.args[1]);
                    else if (state.IR == 0xbe) // Absolute,X / Absolute,Y
                        effectiveAddress = yAddress(state.args[0], state.args[1]);
                    else
                        effectiveAddress = xAddress(state.args[0], state.args[1]);
                    break;
            }
        }



        private int immediateArgs(bool x)
        {
            if (state.emulationFlag) return state.args[0];

            if (x && !state.indexWidthFlag)
                return ((state.args[1] << 8) | state.args[0]) & 0xffff;
            if (!x && !state.mWidthFlag)
                return ((state.args[1] << 8) | state.args[0]) & 0xffff;
            return state.args[0];
        }

        private void incrementPC()
        {
            if (state.PC == 0xffff)
                state.PC = 0;
            else
                ++state.PC;
        }
        /// <summary>
        /// Given a single byte, compute the Zero Page,Y offset address.
        /// </summary>
        private int zpyAddress(int zp) => (zp + state.Y) & screen.maskXWidth();
        /// <summary>
        /// Given a single byte, compute the Zero Page, X offset address.
        /// </summary>
        private int zpxAddress(int zp) => (zp + state.X) & screen.maskXWidth();
        /// <summary>
        /// Given a hi byte and a low byte, return the Absolute,X offset address.
        /// </summary>
        /// <param name="lowByte"></param>
        /// <param name="hiByte"></param>
        /// <returns></returns>
        private int xAddress(int lowByte, int hiByte) => (Memory.address(lowByte, hiByte) + state.X) & 0xffff;
        /// <summary>
        /// Given a hi byte and a low byte, return the Absolute,Y offset address.
        /// </summary>
        /// <param name="lowByte"></param>
        /// <param name="hiByte"></param>
        /// <returns></returns>
        private int yAddress(int lowByte, int hiByte) => (Memory.address(lowByte, hiByte) + state.Y) & 0xffff;

        /// <summary>
        /// Return a formatted string representing the last instruction and
        /// operands that were executed.
        /// </summary>
        /// <returns>
        /// A string representing the mnemonic and operands of the instruction
        /// </returns>
        public static string disassembleOp(int opCode, int[] args, int insnLen)
        {
            var mnemonic = opcodeNames[opCode];

            if (mnemonic == null) return "???";

            var sb = new StringBuilder(mnemonic);

            switch (instructionModes[opCode])
            {
                case Mode.ABS:
                    sb.Append($" ${Memory.address(args[0], args[1]):X4}");
                    break;
                case Mode.AIX:
                    sb.Append($" (${Memory.address(args[0], args[1]):X4},X)");
                    break;
                case Mode.ABX:
                    sb.Append($" ${Memory.address(args[0], args[1]):X4},X");
                    break;
                case Mode.ABY:
                    sb.Append($" ${Memory.address(args[0], args[1]):X4},Y");
                    break;
                case Mode.IMM:
                    sb.Append(" #$").Append(insnLen > 2 ? $"{Memory.address(args[0], args[1]):X4}" : $"{args[0]:X2}");
                    break;
                case Mode.IND:
                    sb.Append($" (${Memory.address(args[0], args[1]):X4})");
                    break;
                case Mode.XIN:
                    sb.Append($" (${args[0]:X2}),X");
                    break;
                case Mode.INY:
                    sb.Append($" (${args[0]:X2}),Y");
                    break;
                case Mode.REL:
                case Mode.ZPR:
                case Mode.ZPG:
                    sb.Append($" ${args[0]:X2}");
                    break;
                case Mode.ZPX:
                    sb.Append($" ${args[0]:X2},X");
                    break;
                case Mode.ZPY:
                    sb.Append($" ${args[0]:X2},Y");
                    break;
            }
            return sb.ToString();
        }

        public void linkDebugger(Debugger deb)
        {
            Log.nf("%% debugger connected.");
            debugger = deb;
        }

        public void Dispose()
        {
            this.CPUSpin.Reset();
            this.CPUSpin = default;
            this.debugger = null;
            this.instructor = null;
            this.screen = null;
        }
    }
}
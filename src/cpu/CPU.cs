namespace vm.cpu
{
    using System.Drawing;
    using System.Text;
    using components;
    using RC.Framework.Screens;
    using tables;
    using Screen = devices.Screen;

    public class CPU : MemoryTable
    {
        public Screen screen { get; set; }
        public Instructor instructor { get; set; }
        public Stack stack { get; set; }
        public Debugger debugger { get; set; }
        
        #region Flags

        public bool carryFlag
        {
            get => state.carryFlag;
            set => state.carryFlag = value;
        }
        public bool zeroFlag
        {
            get => state.zeroFlag;
            set => state.zeroFlag = value;
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
                zeroFlag = (value & P_ZERO) != 0;
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
            return (this.state.PC + (byte)offset) & 0xffff;
        }

        public void setProgramCounter(int addr)
        {
            this.state.PC = addr;

            // As a side-effect of setting the program counter,
            // we want to peek ahead at the next state.
            peekAhead();
        }
        public CPU()
        {
            screen      = new Screen(this);
            instructor  = new Instructor(this);
            stack       = new Stack(this);
        }

        public void cycle()
        {
            if (state.signalStop) return;
            state.lastPc = state.PC;

            // Check for Interrupts before doing anything else.
            // This will set the PC and jump to the interrupt vector.
            if (state.nmiAsserted)
                debugger?.handleNmi();
            else if (state.irqAsserted)
            {
                if (!state.intWait && !IrqDisableFlag)
                    debugger?.handleIrq(state.PC);
                state.intWait = false;
            }

            // Fetch memory location for this instruction.
            state.IR = Bus.read(state.PC, true);
            var irAddressMode = (state.IR >> 2) & 0x07;  // Bits 3-5 of IR:  [ | | |X|X|X| | ]
            var irOpMode = state.IR & 0x03;              // Bits 6-7 of IR:  [ | | | | | |X|X]

            incrementPC();
            opTrap = false;


            // Decode the instruction and operands
            state.instSize = state.getInstructionSize(state.IR);
            for (var i = 0; i < state.instSize - 1; i++)
            {
                state.args[i] = Bus.read(state.PC, true);
                // Increment PC after reading
                incrementPC();
            }

            state.stepCounter++;

            // Get the data from the effective address (if any)
            int effectiveAddress = 0;
            int tmp; // Temporary storage

            switch (irOpMode)
            {
                case 0:
                case 2:
                    cycleAddress(ref effectiveAddress, ref irAddressMode);
                    break;
                case 3: // Rockwell/WDC 65C02
                    switch (irAddressMode)
                    {
                        case 3:
                            if (((this.state.IR >> 5) & 1) == 0)
                            { // Zero Page
                                effectiveAddress = this.state.args[0];
                            }
                            else
                            { // Absolute
                                effectiveAddress = Memory.address(this.state.args[0], this.state.args[1]);
                            }
                            break;
                        case 7:
                            if (((this.state.IR >> 5) & 1) == 0)
                            { // Zero Page, X
                                effectiveAddress = zpxAddress(this.state.args[0]);
                            }
                            else
                            { // Absolute, X
                                effectiveAddress = xAddress(this.state.args[0], this.state.args[1]);
                            }
                            break;
                        case 0: // stk,S
                            effectiveAddress = this.state.args[0] + this.state.SP & 0xffff;
                            break;
                        case 4: // (stk,S),Y
                            effectiveAddress = this.state.args[0] + this.state.SP & 0xffff;
                            effectiveAddress = yAddress(this.Bus.read(effectiveAddress, true),
                                    this.Bus.read(effectiveAddress + 1, true));
                            break;
                        case 1: // r,R
                            effectiveAddress = this.state.args[0] + this.state.R & 0xffff;
                            break;
                        case 5:// (r,R),Y
                            effectiveAddress = this.state.args[0] + this.state.R & 0xffff;
                            effectiveAddress = yAddress(this.Bus.read(effectiveAddress, true),
                                    this.Bus.read(effectiveAddress + 1, true));
                            break;
                    }
                    break;
                case 1:
                    switch (irAddressMode)
                    {
                        case 0: // (Zero Page,X)
                            tmp = (this.state.args[0] + this.state.X) & 0xff;
                            effectiveAddress = Memory.address(this.Bus.read(tmp, true), this.Bus.read(tmp + 1, true));
                            break;
                        case 1: // Zero Page
                            effectiveAddress = this.state.args[0];
                            break;
                        case 2: // #Immediate
                            effectiveAddress = -1;
                            break;
                        case 3: // Absolute
                            effectiveAddress = Memory.address(this.state.args[0], this.state.args[1]);
                            break;
                        case 4: // (Zero Page),Y
                            tmp = Memory.address(this.Bus.read(this.state.args[0], true),
                                          this.Bus.read((this.state.args[0] + 1) & 0xff, true));
                            effectiveAddress = (tmp + this.state.Y) & 0xffff;
                            break;
                        case 5: // Zero Page,X
                            effectiveAddress = zpxAddress(this.state.args[0]);
                            break;
                        case 6: // Absolute, Y
                            effectiveAddress = yAddress(this.state.args[0], this.state.args[1]);
                            break;
                        case 7: // Absolute, X
                            effectiveAddress = xAddress(this.state.args[0], this.state.args[1]);
                            break;
                    }
                    break;
            }
             // Execute
            switch (this.state.IR)
            {

                /** Single Byte Instructions; Implied and Relative **/
                case 0x00: // BRK - Force Interrupt - Implied
                    debugger?.handleBrk(this.state.PC + 1);
                    break;
                case 0x08: // PHP - Push Processor Status - Implied
                    // Break flag is always set in the stack value.
                    stack.PushByte(this.state.getStatusFlag());
                    break;
                case 0x10: // BPL - Branch if Positive - Relative
                    if (!negativeFlag)
                    {
                        this.state.PC = relAddress(this.state.args[0]);
                    }

                    break;
                case 0x18: // CLC - Clear Carry Flag - Implied
                    carryFlag = false;
                    break;
                case 0x20: // JSR - Jump to Subroutine - Implied
                    stack.PushWord(this.state.PC - 1);
                    this.state.PC = Memory.address(this.state.args[0], this.state.args[1]);
                    break;
                case 0xfc: // JSR - (Absolute Indexed Indirect,X)
                    stack.PushWord(this.state.PC - 1);
                    tmp = (((this.state.args[1] << 8) | this.state.args[0]) + this.state.X) & 0xffff;
                    this.state.PC = Memory.address(this.Bus.read(tmp, true), this.Bus.read(tmp + 1, true));
                    break;
                case 0x28: // PLP - Pull Processor Status - Implied
                    ProcessorStatus = (stack.PopByte());
                    break;
                case 0x30: // BMI - Branch if Minus - Relative
                    if (negativeFlag)
                    {
                        this.state.PC = relAddress(this.state.args[0]);
                    }

                    break;
                case 0x38: // SEC - Set Carry Flag - Implied
                    carryFlag = true;
                    break;
                case 0x40: // RTI - Return from Interrupt - Implied
                    ProcessorStatus = (stack.PopByte());
                    int lo = stack.PopByte();
                    int hi = stack.PopByte();
                    setProgramCounter(Memory.address(lo, hi));
                    break;
                case 0x48: // PHA - Push Accumulator - Implied
                    stack.Push(this.state.A, false);
                    break;
                case 0x50: // BVC - Branch if Overflow Clear - Relative
                    if (!overFlowFlag)
                    {
                        this.state.PC = relAddress(this.state.args[0]);
                    }

                    break;
                case 0x58: // CLI - Clear Interrupt Disable - Implied
                    IrqDisableFlag = false;
                    break;
                case 0x5a: // 65C02 PHY - Push Y to stack
                    stack.Push(this.state.Y, true);
                    break;
                case 0x60: // RTS - Return from Subroutine - Implied
                    lo = stack.PopByte();
                    hi = stack.PopByte();
                    setProgramCounter((Memory.address(lo, hi) + 1) & 0xffff);
                    break;
                case 0x68: // PLA - Pull Accumulator - Implied
                    this.state.A = stack.Pop(false);
                    instructor.setArithmeticFlags(this.state.A, false);
                    break;
                case 0x70: // BVS - Branch if Overflow Set - Relative
                    if (overFlowFlag)
                    {
                        this.state.PC = relAddress(this.state.args[0]);
                    }

                    break;
                case 0x78: // SEI - Set Interrupt Disable - Implied
                    IrqDisableFlag = true;
                    break;
                case 0x7a: // 65C02 PLY - Pull Y from Stack
                    this.state.Y = stack.Pop(true);
                    instructor.setArithmeticFlags(this.state.Y, true);
                    break;
                case 0x80: // 65C02 BRA - Branch Always
                    this.state.PC = relAddress(this.state.args[0]);
                    break;
                case 0x88: // DEY - Decrement Y Register - Implied
                    this.state.Y = --this.state.Y & screen.maskXWidth();
                    instructor.setArithmeticFlags(this.state.Y, true);
                    break;
                case 0x8a: // TXA - Transfer X to Accumulator - Implied
                    this.state.A = this.state.X;
                    instructor.setArithmeticFlags(this.state.A, false);
                    break;
                case 0x90: // BCC - Branch if Carry Clear - Relative
                    if (!carryFlag)
                    {
                        this.state.PC = relAddress(this.state.args[0]);
                    }

                    break;
                case 0x98: // TYA - Transfer Y to Accumulator - Implied
                    this.state.A = this.state.Y;
                    instructor.setArithmeticFlags(this.state.A, false);
                    break;
                case 0x9a: // TXS - Transfer X to Stack Pointer - Implied
                    if (this.state.indexWidthFlag)
                    {
                        this.state.SP = (this.state.SP & 0xff00 | (this.state.X & 0xff));
                        
                    }
                    else
                    {
                        this.state.SP = (this.state.X);
                    }

                    break;
                case 0xa8: // TAY - Transfer Accumulator to Y - Implied
                    this.state.Y = this.state.A;
                    instructor.setArithmeticFlags(this.state.Y, true);
                    break;
                case 0xaa: // TAX - Transfer Accumulator to X - Implied
                    this.state.X = this.state.A;
                    instructor.setArithmeticFlags(this.state.X, true);
                    break;
                case 0x9b: // TXY - Transfer X to Y
                    this.state.Y = this.state.X;
                    instructor.setArithmeticFlags(this.state.Y, true);
                    break;
                case 0xbb: // TYX - Transfer Y to X
                    this.state.X = this.state.Y;
                    instructor.setArithmeticFlags(this.state.X, true);
                    break;
                case 0xb0: // BCS - Branch if Carry Set - Relative
                    if (carryFlag)
                    {
                        this.state.PC = relAddress(this.state.args[0]);
                    }

                    break;
                case 0xb8: // CLV - Clear Overflow Flag - Implied
                    overFlowFlag = false;
                    break;
                case 0xba: // TSX - Transfer Stack Pointer to X - Implied
                    this.state.X = this.state.SP;
                    instructor.setArithmeticFlags(this.state.X, true);
                    break;
                case 0xc8: // INY - Increment Y Register - Implied
                    this.state.Y = ++this.state.Y & screen.maskXWidth();
                    instructor.setArithmeticFlags(this.state.Y, true);
                    break;
                case 0xca: // DEX - Decrement X Register - Implied
                    this.state.X = --this.state.X & screen.maskXWidth();
                    instructor.setArithmeticFlags(this.state.X, true);
                    break;
                case 0xd0: // BNE - Branch if Not Equal to Zero - Relative
                    if (!zeroFlag)
                    {
                        this.state.PC = relAddress(this.state.args[0]);
                    }

                    break;
                case 0xd8: // CLD - Clear Decimal Mode - Implied
                    DecimalModeFlag = false;
                    break;
                case 0xda: // 65C02 PHX - Push X to stack
                    stack.Push(this.state.X, true);
                    break;
                case 0xe8: // INX - Increment X Register - Implied
                    this.state.X = ++this.state.X & screen.maskXWidth();
                    instructor.setArithmeticFlags(this.state.X, true);
                    break;
                case 0xea: // NOP
                    // Do nothing.
                    break;
                case 0xf0: // BEQ - Branch if Equal to Zero - Relative
                    if (zeroFlag)
                    {
                        this.state.PC = relAddress(this.state.args[0]);
                    }

                    break;
                case 0xf8: // SED - Set Decimal Flag - Implied
                    DecimalModeFlag = true;
                    break;
                case 0xfa: // 65C02 PLX - Pull X from Stack
                    this.state.X = stack.Pop(true);
                    instructor.setArithmeticFlags(this.state.X, true);
                    break;

                case 0x62: // PER
                    stack.PushWord(this.state.args[0] + this.state.PC);
                    break;
                case 0xd4: // PEI
                    stack.PushWord(
                        this.Bus.read(this.state.args[0], true) | (this.Bus.read(this.state.args[0] + 1, true) << 8));
                    break;
                case 0xf4: // PEA
                    stack.PushWord(Memory.address(this.state.args[0], this.state.args[1]));
                    break;

                case 0xeb: // XBA - Exchange A bytes
                    if (this.state.mWidthFlag)
                    {
                        tmp = this.state.A_TOP >> 8;
                        this.state.A_TOP = this.state.A << 8;
                        this.state.A = tmp;
                    }
                    else
                    {
                        this.state.A = ((this.state.A & 0xff) << 8) | ((this.state.A >> 8) & 0xff);
                    }

                    break;
                case 0xfb: // XCE - Exchange Carry and Emulation flags
                    bool oldCarry = this.state.carryFlag;
                    this.state.carryFlag = this.state.emulationFlag;
                    this.state.emulationFlag = oldCarry;
                    if (oldCarry)
                    {
                        if (!this.state.mWidthFlag)
                        {
                            this.state.A_TOP = this.state.A & 0xff00;
                        }

                        this.state.mWidthFlag = this.state.indexWidthFlag = true;
                        this.state.A &= 0xff;
                        this.state.X &= 0xff;
                        this.state.Y &= 0xff;
                    }

                    break;

                case 0xc2: // REP - Reset status bits
                    ProcessorStatus = (int)(ProcessorStatus & (state.args[0] ^ 0xffffffff));
                    break;
                case 0xe2: // SEP - Set status bits
                    ProcessorStatus = (ProcessorStatus | this.state.args[0]);
                    break;

                case 0xdb: // STP
                    this.state.signalStop = true;
                    break;
                case 0xcb: // WAI
                    this.state.intWait = true;
                    break;

                case 0xef: // MMU
                    switch (this.state.args[0])
                    {
                        case 0x00: // Map device in Reg A to redbus window
                            this.Bus.RedBus.activeDeviceID = (this.state.A & 0xff);
                            break;
                        case 0x80: // Get mapped device to A
                            this.state.A = this.Bus.RedBus.activeDeviceID;
                            break;

                        case 0x01: // Redbus Window offset to A
                            this.Bus.RedBus.WindowsOffset = (this.state.A);
                            break;
                        case 0x81: // Get RB window offset to A
                            this.state.A = this.Bus.RedBus.WindowsOffset;
                            if (this.state.mWidthFlag)
                            {
                                this.state.A_TOP = this.state.A & 0xff00;
                                this.state.A &= 0xff;
                            }

                            break;

                        case 0x02: // Enable redbus
                            this.Bus.RedBus.Enable();
                            break;
                        case 0x82: // Disable redbus
                            this.Bus.RedBus.Disable();
                            break;

                        case 0x03: // Set external memory mapped window to A
                            this.Bus.RedBus.MemoryWindow = (this.state.A);
                            break;
                        case 0x83: // Get memory mapped window to A
                            this.state.A = this.Bus.RedBus.MemoryWindow;
                            if (this.state.mWidthFlag)
                            {
                                this.state.A_TOP = this.state.A & 0xff00;
                                this.state.A &= 0xff;
                            }

                            break;

                        case 0x04: // Enable external memory mapped window
                            this.Bus.RedBus.enableWindow = (true);
                            break;
                        case 0x84: // Disable external memory mapped window
                            this.Bus.RedBus.enableWindow = (false);
                            break;

                        case 0x05: // Set BRK address to A
                            this.state.BRK = this.state.A;
                            break;
                        case 0x85: // Get BRK address to A
                            this.state.A = this.state.BRK;
                            if (this.state.mWidthFlag)
                            {
                                this.state.A_TOP = this.state.A & 0xff00;
                                this.state.A &= 0xff;
                            }

                            break;

                        case 0x06: // Set POR address to A
                            this.state.POR = this.state.A;
                            break;
                        case 0x86: // Get POR address to A
                            this.state.A = this.state.POR;
                            if (this.state.mWidthFlag)
                            {
                                this.state.A_TOP = this.state.A & 0xff00;
                                this.state.A &= 0xff;
                            }

                            break;

                        case 0xff: // Output A register to MC logfile
                            logCallback?.Invoke($"A:{this.state.A}");
                            break;
                    }

                    break;

                case 0x22: // ENT - Enter word, RHI, I=PC+2, PC=(PC)
                    stack.RPushWord(this.state.I);
                    this.state.I = this.state.PC + 2;
                    this.state.PC = readMemory(this.state.PC, false);
                    break;
                case 0x42: // NXA - Next word into A, A=(I), I=I+1/I=I+2
                    this.state.A = readMemory(this.state.I, false);
                    this.state.I += this.state.mWidthFlag ? 1 : 2;
                    break;
                case 0x02: // NXT - Next word, PC=(I), I=I+2
                    this.state.PC = readMemory(this.state.I, false);
                    this.state.I += 2;
                    break;
                case 0x8b: // TXR - Transfer X to R
                    if (this.state.indexWidthFlag)
                    {
                        this.state.R = (this.state.R & 0xff00) | (this.state.X & 0xff);
                    }
                    else
                    {
                        this.state.R = this.state.X;
                    }

                    instructor.setArithmeticFlags(this.state.R, true);
                    break;
                case 0xab: // TRX - Transfer R to X
                    this.state.X = this.state.R;
                    instructor.setArithmeticFlags(this.state.X, true);
                    break;
                case 0x5c: // TXI - Transfer X to I
                    this.state.I = this.state.X;
                    instructor.setArithmeticFlags(this.state.X, true);
                    break;
                case 0xdc: // TIX - Transfer I to X
                    this.state.X = this.state.I;
                    instructor.setArithmeticFlags(this.state.X, true);
                    break;
                case 0x4b: // RHA - Push accumulator to R stack
                    stack.RPush(this.state.A, false);
                    break;
                case 0x6b: // RLA - Pull accumulator from R stack
                    this.state.A = stack.RPop(false);
                    instructor.setArithmeticFlags(this.state.A, false);
                    break;
                case 0x1b: // RHX - Push X register to R stack
                    stack.RPush(this.state.X, true);
                    break;
                case 0x3b: // RLX - Pull X register from R stack
                    this.state.X = stack.RPop(true);
                    instructor.setArithmeticFlags(this.state.X, true);
                    break;
                case 0x5b: // RHY - Push Y register to R stack
                    stack.RPush(this.state.Y, true);
                    break;
                case 0x7b: // RLY - Pull Y register from R stack
                    this.state.Y = stack.RPop(true);
                    instructor.setArithmeticFlags(this.state.Y, true);
                    break;
                case 0x0b: // RHI - Push I register to R stack
                    stack.RPushWord(this.state.I);
                    break;
                case 0x2b: // RLI - Pull I register from R stack
                    this.state.I = stack.RPopWord();
                    instructor.setArithmeticFlags(this.state.I, true);
                    break;
                case 0x82: // RER - Push effective relative address to R stack
                    stack.RPushWord(this.state.PC + this.state.args[0]);
                    break;

                // Undocumented. See http://bigfootinformatika.hu/65el02/archive/65el02_instructions.txt
                case 0x44: // REA - push address to R stack
                    stack.RPushWord(Memory.address(this.state.args[0], this.state.args[1]));
                    break;
                case 0x54: // REI - push indirect zp address to R stack
                    stack.RPushWord(readWord(this.state.args[0]));
                    break;

                // MUL - Signed multiply A into D:A
                case 0x0f: // Zp
                case 0x1f: // Zp,X
                case 0x2f: // Abs
                case 0x3f: // Abs, X
                    instructor.mul(readMemory(effectiveAddress, false));
                    break;

                // DIV - Signed divide D:A, quotient in A, remainder in D
                case 0x4f: // Zp
                case 0x5f: // Zp, X
                case 0x6f: // Abs
                case 0x7f: // Abs, X
                    instructor.div(readMemory(effectiveAddress, false));
                    break;

                case 0x8f: // ZEA - Zero extend A into D:A
                    this.state.D = 0;
                    this.state.A_TOP = 0;
//                this.state.A &= 0xff; // b = 0
                    break;
                case 0x9f: // SEA - Sign extend A into D:A
                    this.state.D = (this.state.A & this.screen.negativeMWidth()) == 0 ? 0 : 0xffff;
                    this.state.A_TOP = (this.state.D & 0xff) << 8;
//                this.state.A = ((this.state.D & 0xff) << 8) | (this.state.A & 0xff);
                    break;
                case 0xaf: // TDA - Transfer D to A
                    this.state.A = this.state.D & this.screen.maskMWidth();
                    instructor.setArithmeticFlags(this.state.A, false);
                    break;
                case 0xbf: // TAD - Transfer A to D
                    if (this.state.mWidthFlag)
                    {
                        this.state.D = this.state.A_TOP | (this.state.A & 0xff);
                    }
                    else
                    {
                        this.state.D = this.state.A;
                    }

                    instructor.setArithmeticFlags(this.state.A, false);
                    break;
                case 0xcf: // PLD - Pull D register from stack
                    this.state.D = stack.Pop(false);
                    instructor.setArithmeticFlags(this.state.D, false);
                    break;
                case 0xdf: // PHD - Push D register on stack
                    stack.Push(this.state.D, false);
                    break;



                /** JMP *****************************************************************/
                case 0x4c: // JMP - Absolute
                    this.state.PC = Memory.address(this.state.args[0], this.state.args[1]);
                    break;
                case 0x6c: // JMP - Indirect
                    lo = Memory.address(this.state.args[0], this.state.args[1]); // Address of low byte
                    this.state.PC = Memory.address(this.Bus.read(lo, true), this.Bus.read(lo + 1, true));
                    break;
                case 0x7c: // 65C02 JMP - (Absolute Indexed Indirect,X)
                    lo = (((this.state.args[1] << 8) | this.state.args[0]) + this.state.X) & 0xffff;
                    hi = lo + 1;
                    this.state.PC = Memory.address(this.Bus.read(lo, true), this.Bus.read(hi, true));
                    break;

                /** ORA - Logical Inclusive Or ******************************************/
                case 0x09: // #Immediate
                    this.state.A |= immediateArgs(false);
                    instructor.setArithmeticFlags(this.state.A, false);
                    break;
                case 0x12: // 65C02 ORA (ZP)
                case 0x01: // (Zero Page,X)
                case 0x05: // Zero Page
                case 0x0d: // Absolute
                case 0x11: // (Zero Page),Y
                case 0x15: // Zero Page,X
                case 0x19: // Absolute,Y
                case 0x1d: // Absolute,X
                case 0x03: // stk,S
                case 0x13: // (stk,S),Y
                case 0x07: // r,R
                case 0x17: // (r,R),Y
                    this.state.A |= readMemory(effectiveAddress, false);
                    instructor.setArithmeticFlags(this.state.A, false);
                    break;


                /** ASL - Arithmetic Shift Left *****************************************/
                case 0x0a: // Accumulator
                    this.state.A = instructor.asl(this.state.A);
                    instructor.setArithmeticFlags(this.state.A, false);
                    break;
                case 0x06: // Zero Page
                case 0x0e: // Absolute
                case 0x16: // Zero Page,X
                case 0x1e: // Absolute,X
                    tmp = instructor.asl(readMemory(effectiveAddress, false));
                    writeMemory(effectiveAddress, tmp, false);
                    instructor.setArithmeticFlags(tmp, false);
                    break;


                /** BIT - Bit Test ******************************************************/
                case 0x89: // 65C02 #Immediate
                    instructor.setZeroFlag((this.state.A & immediateArgs(false)) == 0);
                    break;
                case 0x34: // 65C02 Zero Page,X
                case 0x24: // Zero Page
                case 0x2c: // Absolute
                case 0x3c: // Absolute,X
                    tmp = readMemory(effectiveAddress, false);
                    instructor.setZeroFlag((this.state.A & tmp) == 0);
                    instructor.setNegativeFlag((tmp & this.screen.negativeMWidth()) != 0);
                    instructor.setOverflowFlag((tmp & (this.state.mWidthFlag ? 0x40 : 0x4000)) != 0);
                    break;


                /** AND - Logical AND ***************************************************/
                case 0x29: // #Immediate
                    this.state.A &= immediateArgs(false);
                    instructor.setArithmeticFlags(this.state.A, false);
                    break;
                case 0x32: // 65C02 AND (ZP)
                case 0x21: // (Zero Page,X)
                case 0x25: // Zero Page
                case 0x2d: // Absolute
                case 0x31: // (Zero Page),Y
                case 0x35: // Zero Page,X
                case 0x39: // Absolute,Y
                case 0x3d: // Absolute,X
                case 0x23: // stk,S
                case 0x33: // (stk,S),Y
                case 0x27: // r,R
                case 0x37: // (r,R),Y
                    this.state.A &= readMemory(effectiveAddress, false);
                    instructor.setArithmeticFlags(this.state.A, false);
                    break;


                /** ROL - Rotate Left ***************************************************/
                case 0x2a: // Accumulator
                    this.state.A = instructor.rol(this.state.A);
                    instructor.setArithmeticFlags(this.state.A, false);
                    break;
                case 0x26: // Zero Page
                case 0x2e: // Absolute
                case 0x36: // Zero Page,X
                case 0x3e: // Absolute,X
                    tmp = instructor.rol(readMemory(effectiveAddress, false));
                    writeMemory(effectiveAddress, tmp, false);
                    instructor.setArithmeticFlags(tmp, false);
                    break;


                /** EOR - Exclusive OR **************************************************/
                case 0x49: // #Immediate
                    this.state.A ^= immediateArgs(false);
                    instructor.setArithmeticFlags(this.state.A, false);
                    break;
                case 0x52: // 65C02 EOR (ZP)
                case 0x41: // (Zero Page,X)
                case 0x45: // Zero Page
                case 0x4d: // Absolute
                case 0x51: // (Zero Page,Y)
                case 0x55: // Zero Page,X
                case 0x59: // Absolute,Y
                case 0x5d: // Absolute,X
                case 0x43: // stk,S
                case 0x53: // (stk,S),Y
                case 0x47: // r,R
                case 0x57: // (r,R),Y
                    this.state.A ^= readMemory(effectiveAddress, false);
                    instructor.setArithmeticFlags(this.state.A, false);
                    break;


                /** LSR - Logical Shift Right *******************************************/
                case 0x4a: // Accumulator
                    this.state.A = instructor.lsr(this.state.A);
                    instructor.setArithmeticFlags(this.state.A, false);
                    break;
                case 0x46: // Zero Page
                case 0x4e: // Absolute
                case 0x56: // Zero Page,X
                case 0x5e: // Absolute,X
                    tmp = instructor.lsr(readMemory(effectiveAddress, false));
                    writeMemory(effectiveAddress, tmp, false);
                    instructor.setArithmeticFlags(tmp, false);
                    break;


                /** ADC - Add with Carry ************************************************/
                case 0x69: // #Immediate
                    this.state.A = 
                        this.state.decimalModeFlag ? 
                            this.instructor.ADCDecimal(
                                this.state.A, immediateArgs(false)) :
                            this.instructor.ADC(this.state.A, immediateArgs(false));

                    break;
                case 0x72: // 65C02 ADC (ZP)
                case 0x61: // (Zero Page,X)
                case 0x65: // Zero Page
                case 0x6d: // Absolute
                case 0x71: // (Zero Page),Y
                case 0x75: // Zero Page,X
                case 0x79: // Absolute,Y
                case 0x7d: // Absolute,X
                case 0x63: // stk,S
                case 0x73: // (stk,S),Y
                case 0x67: // r,R
                case 0x77: // (r,R),Y
                    if (this.state.decimalModeFlag)
                    {
                        this.state.A = instructor.ADCDecimal(this.state.A, readMemory(effectiveAddress, false));
                    }
                    else
                    {
                        this.state.A = instructor.ADC(this.state.A, readMemory(effectiveAddress, false));
                    }

                    break;


                /** ROR - Rotate Right **************************************************/
                case 0x6a: // Accumulator
                    this.state.A = instructor.ror(this.state.A);
                    instructor.setArithmeticFlags(this.state.A, false);
                    break;
                case 0x66: // Zero Page
                case 0x6e: // Absolute
                case 0x76: // Zero Page,X
                case 0x7e: // Absolute,X
                    tmp = instructor.ror(readMemory(effectiveAddress, false));
                    writeMemory(effectiveAddress, tmp, false);
                    instructor.setArithmeticFlags(tmp, false);
                    break;


                /** STA - Store Accumulator *********************************************/
                case 0x92: // 65C02 STA (ZP)
                case 0x81: // (Zero Page,X)
                case 0x85: // Zero Page
                case 0x8d: // Absolute
                case 0x91: // (Zero Page),Y
                case 0x95: // Zero Page,X
                case 0x99: // Absolute,Y
                case 0x9d: // Absolute,X
                case 0x83: // stk,S
                case 0x93: // (stk,S),Y
                case 0x87: // r,R
                case 0x97: // (r,R),Y
                    writeMemory(effectiveAddress, this.state.A, false);
                    break;


                /** STY - Store Y Register **********************************************/
                case 0x84: // Zero Page
                case 0x8c: // Absolute
                case 0x94: // Zero Page,X
                    writeMemory(effectiveAddress, this.state.Y, true);
                    break;


                /** STX - Store X Register **********************************************/
                case 0x86: // Zero Page
                case 0x8e: // Absolute
                case 0x96: // Zero Page,Y
                    writeMemory(effectiveAddress, this.state.X, true);
                    break;

                /** STZ - 65C02 Store Zero ****************************************************/
                case 0x64: // Zero Page
                case 0x74: // Zero Page,X
                case 0x9c: // Absolute
                case 0x9e: // Absolute,X
                    writeMemory(effectiveAddress, 0, false);
                    break;

                /** LDY - Load Y Register ***********************************************/
                case 0xa0: // #Immediate
                    this.state.Y = immediateArgs(true);
                    instructor.setArithmeticFlags(this.state.Y, true);
                    break;
                case 0xa4: // Zero Page
                case 0xac: // Absolute
                case 0xb4: // Zero Page,X
                case 0xbc: // Absolute,X
                    this.state.Y = readMemory(effectiveAddress, true);
                    instructor.setArithmeticFlags(this.state.Y, true);
                    break;


                /** LDX - Load X Register ***********************************************/
                case 0xa2: // #Immediate
                    this.state.X = immediateArgs(true);
                    instructor.setArithmeticFlags(this.state.X, true);
                    break;
                case 0xa6: // Zero Page
                case 0xae: // Absolute
                case 0xb6: // Zero Page,Y
                case 0xbe: // Absolute,Y
                    this.state.X = readMemory(effectiveAddress, true);
                    instructor.setArithmeticFlags(this.state.X, true);
                    break;


                /** LDA - Load Accumulator **********************************************/
                case 0xa9: // #Immediate
                    this.state.A = immediateArgs(false);
                    instructor.setArithmeticFlags(this.state.A, false);
                    break;
                case 0xb2: // 65C02 LDA (ZP)
                case 0xa1: // (Zero Page,X)
                case 0xa5: // Zero Page
                case 0xad: // Absolute
                case 0xb1: // (Zero Page),Y
                case 0xb5: // Zero Page,X
                case 0xb9: // Absolute,Y
                case 0xbd: // Absolute,X
                case 0xa3: // stk,S
                case 0xb3: // (stk,S),Y
                case 0xa7: // r,R
                case 0xb7: // (r,R),Y
                    this.state.A = readMemory(effectiveAddress, false);
                    instructor.setArithmeticFlags(this.state.A, false);
                    break;


                /** CPY - Compare Y Register ********************************************/
                case 0xc0: // #Immediate
                    instructor.cmp(this.state.Y, immediateArgs(true), true);
                    break;
                case 0xc4: // Zero Page
                case 0xcc: // Absolute
                    instructor.cmp(this.state.Y, readMemory(effectiveAddress, true), true);
                    break;


                /** CMP - Compare Accumulator *******************************************/
                case 0xc9: // #Immediate
                    instructor.cmp(this.state.A, immediateArgs(false), false);
                    break;
                case 0xd2: // 65C02 CMP (ZP)
                case 0xc1: // (Zero Page,X)
                case 0xc5: // Zero Page
                case 0xcd: // Absolute
                case 0xd1: // (Zero Page),Y
                case 0xd5: // Zero Page,X
                case 0xd9: // Absolute,Y
                case 0xdd: // Absolute,X
                case 0xc3: // stk,S
                case 0xd3: // (stk,S),Y
                case 0xc7: // r,R
                case 0xd7: // (r,R),Y
                    instructor.cmp(this.state.A, readMemory(effectiveAddress, false), false);
                    break;


                /** DEC - Decrement Memory **********************************************/
                case 0x3a: // 65C02 Immediate
                    this.state.A = --this.state.A & this.screen.maskMWidth();
                    instructor.setArithmeticFlags(this.state.A, false);
                    break;
                case 0xc6: // Zero Page
                case 0xce: // Absolute
                case 0xd6: // Zero Page,X
                case 0xde: // Absolute,X
                    tmp = (readMemory(effectiveAddress, false) - 1) & this.screen.maskMWidth();
                    writeMemory(effectiveAddress, tmp, false);
                    instructor.setArithmeticFlags(tmp, false);
                    break;


                /** CPX - Compare X Register ********************************************/
                case 0xe0: // #Immediate
                    instructor.cmp(this.state.X, immediateArgs(true), true);
                    break;
                case 0xe4: // Zero Page
                case 0xec: // Absolute
                    instructor.cmp(this.state.X, readMemory(effectiveAddress, true), true);
                    break;


                /** SBC - Subtract with Carry (Borrow) **********************************/
                case 0xe9: // #Immediate
                    if (this.state.decimalModeFlag)
                    {
                        this.state.A = instructor.sbcDecimal(this.state.A, immediateArgs(false));
                    }
                    else
                    {
                        this.state.A = instructor.sbc(this.state.A, immediateArgs(false));
                    }

                    break;
                case 0xf2: // 65C02 SBC (ZP)
                case 0xe1: // (Zero Page,X)
                case 0xe5: // Zero Page
                case 0xed: // Absolute
                case 0xf1: // (Zero Page),Y
                case 0xf5: // Zero Page,X
                case 0xf9: // Absolute,Y
                case 0xfd: // Absolute,X
                case 0xe3: // stk,S
                case 0xf3: // (stk,S),Y
                case 0xe7: // r,R
                case 0xf7: // (r,R),Y
                    if (this.state.decimalModeFlag)
                    {
                        this.state.A = instructor.sbcDecimal(this.state.A, readMemory(effectiveAddress, false));
                    }
                    else
                    {
                        this.state.A = instructor.sbc(this.state.A, readMemory(effectiveAddress, false));
                    }

                    break;


                /** INC - Increment Memory **********************************************/
                case 0x1a: // 65C02 Increment Immediate
                    this.state.A = ++this.state.A & this.screen.maskMWidth();
                    instructor.setArithmeticFlags(this.state.A, false);
                    break;
                case 0xe6: // Zero Page
                case 0xee: // Absolute
                case 0xf6: // Zero Page,X
                case 0xfe: // Absolute,X
                    tmp = (readMemory(effectiveAddress, false) + 1) & this.screen.maskMWidth();
                    writeMemory(effectiveAddress, tmp, false);
                    instructor.setArithmeticFlags(tmp, false);
                    break;

                /** 65C02 TRB/TSB - Test and Reset Bit/Test and Set Bit ***************/
                case 0x14: // 65C02 TRB - Test and Reset bit - Zero Page
                case 0x1c: // 65C02 TRB - Test and Reset bit - Absolute
                    tmp = readMemory(effectiveAddress, false);
                    instructor.setZeroFlag((this.state.A & tmp) == 0);
                    tmp = (tmp &= ~(this.state.A)) & this.screen.maskMWidth();
                    writeMemory(effectiveAddress, tmp, false);
                    break;

                case 0x04: // 65C02 TSB - Test and Set bit - Zero Page
                case 0x0c: // 65C02 TSB - Test and Set bit - Absolute
                    tmp = readMemory(effectiveAddress, false);
                    instructor.setZeroFlag((this.state.A & tmp) == 0);
                    tmp = (tmp |= (this.state.A)) & this.screen.maskMWidth();
                    writeMemory(effectiveAddress, tmp, false);
                    break;

                /** Unimplemented Instructions ****************************************/
                default:
                    this.opTrap = true;
                    break;
            }

        }

        private void cycleAddress(ref int effectiveAddress, ref int irAddressMode)
        {
            switch (irAddressMode)
            {
                case 0: // #Immediate
                    break;
                case 1: // Zero Page
                    effectiveAddress = state.args[0];
                    break;
                case 2: // Accumulator - ignored
                    break;
                case 3: // Absolute
                    effectiveAddress = Memory.address(state.args[0], state.args[1]);
                    break;
                case 4: // 65C02 (Zero Page)
                    effectiveAddress = Memory.address(readByte(state.args[0]), readByte((state.args[0] + 1) & 0xff));
                    break;
                case 5: // Zero Page,X / Zero Page,Y
                    if (state.IR == 0x14)
                        effectiveAddress = state.args[0]; // 65C02 TRB Zero Page
                    else if (state.IR == 0x96 || state.IR == 0xb6)
                        effectiveAddress = zpyAddress(state.args[0]);
                    else
                        effectiveAddress = zpxAddress(state.args[0]);
                    break;
                case 7:
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
        private int xAddress(int lowByte, int hiByte) => (Memory.address(lowByte, hiByte) + this.state.X) & 0xffff;
        /// <summary>
        /// Given a hi byte and a low byte, return the Absolute,Y offset address.
        /// </summary>
        /// <param name="lowByte"></param>
        /// <param name="hiByte"></param>
        /// <returns></returns>
        private int yAddress(int lowByte, int hiByte) => (Memory.address(lowByte, hiByte) + this.state.Y) & 0xffff;

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
                    sb.Append(" #$").Append(insnLen > 2 ? $"{Memory.address(args[0], args[1]):X4}" :$"{args[0]:X2}");
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
            this.debugger = deb;
        }
    }
}
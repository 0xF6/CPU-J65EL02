namespace vm.cpu
{
    using System;
    using components;
    using extensions;

    public partial class CPU
    {
        public void cycle()
        {
            opBeginTime = DateTime.Now.Ticks;
            state.lastPc = state.PC;
            if (state.nmiAsserted)
                debugger?.handleNmi();
            else if (state.irqAsserted && !IrqDisableFlag)
                debugger?.handleIrq(state.PC);
            state.IR = Bus.read(state.PC, true);
            var irAddressMode = (state.IR >> 2) & 0x07;  // Bits 3-5 of IR:  [ | | |X|X|X| | ]
            var irOpMode = state.IR & 0x03;              // Bits 6-7 of IR:  [ | | | | | |X|X]
            incrementPC();
            opTrap = false;
            state.instSize = state.getInstructionSize(state.IR);
            for (var i = 0; i < state.instSize - 1; i++)
            {
                state.args[i] = Bus.read(state.PC, true);
                incrementPC();
            }
            state.stepCounter++;

            int effectiveAddress = 0;
            int tmp; // Temporary storage
            var bus = Bus;
            switch (irOpMode)
            {
                case 0:
                case 2:
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
                            if (IsLight)
                            {
                                effectiveAddress = Memory.address(Bus.read(state.args[0], true),
                                                                 Bus.read((state.args[0] + 1) & 0xff, true));
                            }
                            break;
                        case 5: // Zero Page,X / Zero Page,Y
                            if (state.IR == 0x14)
                                effectiveAddress = state.args[0];
                            else if (state.IR == 0x96 || state.IR == 0xb6)
                                effectiveAddress = zpyAddress(state.args[0]);
                            else
                                effectiveAddress = zpxAddress(state.args[0]);
                            break;
                        case 7:
                            if (state.IR == 0x9c || state.IR == 0x1c)
                            { // 65C02 STZ & TRB Absolute
                                effectiveAddress = Memory.address(state.args[0], state.args[1]);
                            }
                            else if (state.IR == 0xbe)
                                effectiveAddress = yAddress(state.args[0], state.args[1]);
                            else
                                effectiveAddress = xAddress(state.args[0], state.args[1]);
                            break;
                    }
                    break;
                case 3: // Rockwell/WDC 65C02
                    switch (irAddressMode)
                    {
                        case 1: // Zero Page
                        case 3:
                        case 5:
                        case 7: // Zero Page, Relative
                            effectiveAddress = state.args[0];
                            break;
                    }
                    break;
                case 1:
                    switch (irAddressMode)
                    {
                        case 0: // (Zero Page,X)
                            tmp = (state.args[0] + state.X) & 0xff;
                            effectiveAddress = Memory.address(bus.read(tmp, true), bus.read(tmp + 1, true));
                            break;
                        case 1: // Zero Page
                            effectiveAddress = state.args[0];
                            break;
                        case 2: // #Immediate
                            effectiveAddress = -1;
                            break;
                        case 3: // Absolute
                            effectiveAddress = Memory.address(state.args[0], state.args[1]);
                            break;
                        case 4: // (Zero Page),Y
                            tmp = Memory.address(bus.read(state.args[0], true),
                                          bus.read((state.args[0] + 1) & 0xff, true));
                            effectiveAddress = (tmp + state.Y) & 0xffff;
                            break;
                        case 5: // Zero Page,X
                            effectiveAddress = zpxAddress(state.args[0]);
                            break;
                        case 6: // Absolute, Y
                            effectiveAddress = yAddress(state.args[0], state.args[1]);
                            break;
                        case 7: // Absolute, X
                            effectiveAddress = xAddress(state.args[0], state.args[1]);
                            break;
                    }
                    break;
            }

            // Execute
        switch (state.IR) {

            /** Single Byte Instructions; Implied and Relative **/
            case 0x00: // BRK - Force Interrupt - Implied
                debugger.handleBrk(state.PC + 1);
                break;
            case 0x08: // PHP - Push Processor Status - Implied
                // Break flag is always set in the stack value.
                stack.Push(state.getStatusFlag() | 0x10);
                break;
            case 0x10: // BPL - Branch if Positive - Relative
                if (!negativeFlag) {
                    state.PC = relAddress(state.args[0]);
                }
                break;
            case 0x18: // CLC - Clear Carry Flag - Implied
                carryFlag = false;
                break;
            case 0x20: // JSR - Jump to Subroutine - Implied
                stack.Push((state.PC - 1 >> 8) & 0xff); // PC high byte
                stack.Push(state.PC - 1 & 0xff);        // PC low byte
                state.PC = Memory.address(state.args[0], state.args[1]);
                break;
            case 0x28: // PLP - Pull Processor Status - Implied
                ProcessorStatus = stack.Pop();
                break;
            case 0x30: // BMI - Branch if Minus - Relative
                if (negativeFlag) {
                    state.PC = relAddress(state.args[0]);
                }
                break;
            case 0x38: // SEC - Set Carry Flag - Implied
                carryFlag = true;
                break;
            case 0x40: // RTI - Return from Interrupt - Implied
                ProcessorStatus = stack.Pop();
                int lo = stack.Pop();
                int hi = stack.Pop();
                setProgramCounter(Memory.address(lo, hi));
                break;
            case 0x48: // PHA - Push Accumulator - Implied
                stack.Push(state.A);
                break;
            case 0x50: // BVC - Branch if Overflow Clear - Relative
                if (!overFlowFlag) {
                    state.PC = relAddress(state.args[0]);
                }
                break;
            case 0x58: // CLI - Clear Interrupt Disable - Implied
                IrqDisableFlag = false;
                break;
            case 0x5a: // 65C02 PHY - Push Y to stack
                if (IsLight
                    ) {
                    break;
                }
                stack.Push(state.Y);
                break;
            case 0x60: // RTS - Return from Subroutine - Implied
                lo = stack.Pop();
                hi = stack.Pop();
                setProgramCounter((Memory.address(lo, hi) + 1) & 0xffff);
                break;
            case 0x68: // PLA - Pull Accumulator - Implied
                state.A = stack.Pop();
                instructor.setArithmeticFlags(state.A);
                break;
            case 0x70: // BVS - Branch if Overflow Set - Relative
                if (overFlowFlag) {
                    state.PC = relAddress(state.args[0]);
                }
                break;
            case 0x78: // SEI - Set Interrupt Disable - Implied
                IrqDisableFlag = true;
                break;
            case 0x7a: // 65C02 PLY - Pull Y from Stack
                if (IsLight
                    ) {
                    break;
                }
                state.Y = stack.Pop(false);
                instructor.setArithmeticFlags(state.Y);
                break;
            case 0x80: // 65C02 BRA - Branch Always
                if (IsLight) state.PC = relAddress(state.args[0]);
                break;
            case 0x88: // DEY - Decrement Y Register - Implied
                state.Y = --state.Y & 0xff;
                instructor.setArithmeticFlags(state.Y);
                break;
            case 0x8a: // TXA - Transfer X to Accumulator - Implied
                state.A = state.X;
                instructor.setArithmeticFlags(state.A);
                break;
            case 0x90: // BCC - Branch if Carry Clear - Relative
                if (!carryFlag) {
                    state.PC = relAddress(state.args[0]);
                }
                break;
            case 0x98: // TYA - Transfer Y to Accumulator - Implied
                state.A = state.Y;
                instructor.setArithmeticFlags(state.A);
                break;
            case 0x9a: // TXS - Transfer X to Stack Pointer - Implied
                state.SP = (state.X);
                break;
            case 0xa8: // TAY - Transfer Accumulator to Y - Implied
                state.Y = state.A;
                instructor.setArithmeticFlags(state.Y);
                break;
            case 0xaa: // TAX - Transfer Accumulator to X - Implied
                state.X = state.A;
                instructor.setArithmeticFlags(state.X);
                break;
            case 0xb0: // BCS - Branch if Carry Set - Relative
                if (carryFlag) {
                    state.PC = relAddress(state.args[0]);
                }
                break;
            case 0xb8: // CLV - Clear Overflow Flag - Implied
                overFlowFlag = false;
                break;
            case 0xba: // TSX - Transfer Stack Pointer to X - Implied
                state.X = state.SP;
                instructor.setArithmeticFlags(state.X);
                break;
            case 0xc8: // INY - Increment Y Register - Implied
                state.Y = ++state.Y & 0xff;
                instructor.setArithmeticFlags(state.Y);
                break;
            case 0xca: // DEX - Decrement X Register - Implied
                state.X = --state.X & 0xff;
                instructor.setArithmeticFlags(state.X);
                break;
            case 0xd0: // BNE - Branch if Not Equal to Zero - Relative
                if (!zeroFlag) {
                    state.PC = relAddress(state.args[0]);
                }
                break;
            case 0xd8: // CLD - Clear Decimal Mode - Implied
                DecimalModeFlag = false;
                break;
            case 0xda: // 65C02 PHX - Push X to stack
                if (IsLight
                    ) {
                    break;
                }
                stack.Push(state.X);
                break;
            case 0xe8: // INX - Increment X Register - Implied
                state.X = ++state.X & 0xff;
                instructor.setArithmeticFlags(state.X);
                break;
            case 0xea: // NOP
                // Do nothing.
                break;
            case 0xf0: // BEQ - Branch if Equal to Zero - Relative
                if (zeroFlag) {
                    state.PC = relAddress(state.args[0]);
                }
                break;
            case 0xf8: // SED - Set Decimal Flag - Implied
                DecimalModeFlag = true;
                break;
            case 0xfa: // 65C02 PLX - Pull X from Stack
                if (IsLight
                    ) {
                    break;
                }
                state.X = stack.Pop();
                instructor.setArithmeticFlags(state.X);
                break;

            /** JMP *****************************************************************/
            case 0x4c: // JMP - Absolute
                state.PC = Memory.address(state.args[0], state.args[1]);
                break;
            case 0x6c: // JMP - Indirect
                lo = Memory.address(state.args[0], state.args[1]); // Address of low byte

                if (state.args[0] == 0xff &&
                    (IsLight
                     )) {
                    hi = Memory.address(0x00, state.args[1]);
                } else {
                    hi = lo + 1;
                }

                state.PC = Memory.address(bus.read(lo, true), bus.read(hi, true));
                /* TODO: For accuracy, allow a flag to enable broken behavior of early 6502s:
                 *
                 * "An original 6502 has does not correctly fetch the target
                 * address if the indirect vector falls on a page boundary
                 * (e.g. $xxFF where xx is and value from $00 to $FF). In this
                 * case fetches the LSB from $xxFF as expected but takes the MSB
                 * from $xx00. This is fixed in some later chips like the 65SC02
                 * so for compatibility always ensure the indirect vector is not
                 * at the end of the page."
                 * (http://www.obelisk.demon.co.uk/6502/reference.html#JMP)
                 */
                break;
            case 0x7c: // 65C02 JMP - (Absolute Indexed Indirect,X)
                if (IsLight
                    ) {
                    break;
                }
                lo = (((state.args[1] << 8) | state.args[0]) + state.X) & 0xffff;
                hi = lo + 1;
                state.PC = Memory.address(bus.read(lo, true), bus.read(hi, true));
                break;

            /** ORA - Logical Inclusive Or ******************************************/
            case 0x09: // #Immediate
                state.A |= state.args[0];
                instructor.setArithmeticFlags(state.A);
                break;
            case 0x12: // 65C02 ORA (ZP)
                if (IsLight
                    ) {
                    break;
                }
                goto case 0x01;
            case 0x01: // (Zero Page,X)
            case 0x05: // Zero Page
            case 0x0d: // Absolute
            case 0x11: // (Zero Page),Y
            case 0x15: // Zero Page,X
            case 0x19: // Absolute,Y
            case 0x1d: // Absolute,X
                state.A |= bus.read(effectiveAddress, true);
                instructor.setArithmeticFlags(state.A);
                break;


            /** ASL - Arithmetic Shift Left *****************************************/
            case 0x0a: // Accumulator
                state.A = instructor.asl(state.A);
                instructor.setArithmeticFlags(state.A);
                break;
            case 0x06: // Zero Page
            case 0x0e: // Absolute
            case 0x16: // Zero Page,X
            case 0x1e: // Absolute,X
                tmp = instructor.asl(bus.read(effectiveAddress, true));
                bus.write(effectiveAddress, tmp);
                instructor.setArithmeticFlags(tmp);
                break;


            /** BIT - Bit Test ******************************************************/
            case 0x89: // 65C02 #Immediate
                instructor.setZeroFlag((state.A & state.args[0]) == 0);
                break;
            case 0x34: // 65C02 Zero Page,X
                if (IsLight
                    ) {
                    break;
                }
                goto case 0x24;
            case 0x24: // Zero Page
            case 0x2c: // Absolute
            case 0x3c: // Absolute,X
                tmp = bus.read(effectiveAddress, true);
                instructor.setZeroFlag((state.A & tmp) == 0);
                instructor.setNegativeFlag((tmp & 0x80) != 0);
                instructor.setOverflowFlag((tmp & 0x40) != 0);
                break;


            /** AND - Logical AND ***************************************************/
            case 0x29: // #Immediate
                state.A &= state.args[0];
                instructor.setArithmeticFlags(state.A);
                break;
            case 0x32: // 65C02 AND (ZP)
                if (IsLight
                    ) {
                    break;
                }

                goto case 0x21;
            case 0x21: // (Zero Page,X)
            case 0x25: // Zero Page
            case 0x2d: // Absolute
            case 0x31: // (Zero Page),Y
            case 0x35: // Zero Page,X
            case 0x39: // Absolute,Y
            case 0x3d: // Absolute,X
                state.A &= bus.read(effectiveAddress, true);
                instructor.setArithmeticFlags(state.A);
                break;


            /** ROL - Rotate Left ***************************************************/
            case 0x2a: // Accumulator
                state.A = instructor.rol(state.A);
                instructor.setArithmeticFlags(state.A);
                break;
            case 0x26: // Zero Page
            case 0x2e: // Absolute
            case 0x36: // Zero Page,X
            case 0x3e: // Absolute,X
                tmp = instructor.rol(bus.read(effectiveAddress, true));
                bus.write(effectiveAddress, tmp);
                instructor.setArithmeticFlags(tmp);
                break;


            /** EOR - Exclusive OR **************************************************/
            case 0x49: // #Immediate
                state.A ^= state.args[0];
                instructor.setArithmeticFlags(state.A);
                break;
            case 0x52: // 65C02 EOR (ZP)
                if (IsLight
                    ) {
                    break;
                }

                goto case 0x41;
            case 0x41: // (Zero Page,X)
            case 0x45: // Zero Page
            case 0x4d: // Absolute
            case 0x51: // (Zero Page,Y)
            case 0x55: // Zero Page,X
            case 0x59: // Absolute,Y
            case 0x5d: // Absolute,X
                state.A ^= bus.read(effectiveAddress, true);
                instructor.setArithmeticFlags(state.A);
                break;


            /** LSR - Logical Shift Right *******************************************/
            case 0x4a: // Accumulator
                state.A = instructor.lsr(state.A);
                instructor.setArithmeticFlags(state.A);
                break;
            case 0x46: // Zero Page
            case 0x4e: // Absolute
            case 0x56: // Zero Page,X
            case 0x5e: // Absolute,X
                tmp = instructor.lsr(bus.read(effectiveAddress, true));
                bus.write(effectiveAddress, tmp);
                instructor.setArithmeticFlags(tmp);
                break;


            /** ADC - Add with Carry ************************************************/
            case 0x69: // #Immediate
                if (state.decimalModeFlag) {
                    state.A = instructor.ADCDecimal(state.A, state.args[0]);
                } else {
                    state.A = instructor.ADC(state.A, state.args[0]);
                }
                break;
            case 0x72: // 65C02 ADC (ZP)
                if (IsLight
                    ) {
                    break;
                }

                goto case 0x61;
            case 0x61: // (Zero Page,X)
            case 0x65: // Zero Page
            case 0x6d: // Absolute
            case 0x71: // (Zero Page),Y
            case 0x75: // Zero Page,X
            case 0x79: // Absolute,Y
            case 0x7d: // Absolute,X
                if (state.decimalModeFlag) {
                    state.A = instructor.ADCDecimal(state.A, bus.read(effectiveAddress, true));
                } else {
                    state.A = instructor.ADC(state.A, bus.read(effectiveAddress, true));
                }
                break;


            /** ROR - Rotate Right **************************************************/
            case 0x6a: // Accumulator
                state.A = instructor.ror(state.A);
                instructor.setArithmeticFlags(state.A);
                break;
            case 0x66: // Zero Page
            case 0x6e: // Absolute
            case 0x76: // Zero Page,X
            case 0x7e: // Absolute,X
                tmp = instructor.ror(bus.read(effectiveAddress, true));
                bus.write(effectiveAddress, tmp);
                instructor.setArithmeticFlags(tmp);
                break;


            /** STA - Store Accumulator *********************************************/
            case 0x92: // 65C02 STA (ZP)
                if (IsLight
                    ) {
                    break;
                }
                goto case 0x81;
            case 0x81: // (Zero Page,X)
            case 0x85: // Zero Page
            case 0x8d: // Absolute
            case 0x91: // (Zero Page),Y
            case 0x95: // Zero Page,X
            case 0x99: // Absolute,Y
            case 0x9d: // Absolute,X
                bus.write(effectiveAddress, state.A);
                break;


            /** STY - Store Y Register **********************************************/
            case 0x84: // Zero Page
            case 0x8c: // Absolute
            case 0x94: // Zero Page,X
                bus.write(effectiveAddress, state.Y);
                break;


            /** STX - Store X Register **********************************************/
            case 0x86: // Zero Page
            case 0x8e: // Absolute
            case 0x96: // Zero Page,Y
                bus.write(effectiveAddress, state.X);
                break;

            /** STZ - 65C02 Store Zero ****************************************************/
            case 0x64: // Zero Page
            case 0x74: // Zero Page,X
            case 0x9c: // Absolute
            case 0x9e: // Absolute,X
                if (IsLight
                    ) {
                    break;
                }
                bus.write(effectiveAddress, 0);
                break;

            /** LDY - Load Y Register ***********************************************/
            case 0xa0: // #Immediate
                state.Y = state.args[0];
                instructor.setArithmeticFlags(state.Y);
                break;
            case 0xa4: // Zero Page
            case 0xac: // Absolute
            case 0xb4: // Zero Page,X
            case 0xbc: // Absolute,X
                state.Y = bus.read(effectiveAddress, true);
                instructor.setArithmeticFlags(state.Y);
                break;


            /** LDX - Load X Register ***********************************************/
            case 0xa2: // #Immediate
                state.X = state.args[0];
                instructor.setArithmeticFlags(state.X);
                break;
            case 0xa6: // Zero Page
            case 0xae: // Absolute
            case 0xb6: // Zero Page,Y
            case 0xbe: // Absolute,Y
                state.X = bus.read(effectiveAddress, true);
                instructor.setArithmeticFlags(state.X);
                break;


            /** LDA - Load Accumulator **********************************************/
            case 0xa9: // #Immediate
                state.A = state.args[0];
                instructor.setArithmeticFlags(state.A);
                break;
            case 0xb2: // 65C02 LDA (ZP)
                if (IsLight
                    ) {
                    break;
                }

                goto case 0xa1;
            case 0xa1: // (Zero Page,X)
            case 0xa5: // Zero Page
            case 0xad: // Absolute
            case 0xb1: // (Zero Page),Y
            case 0xb5: // Zero Page,X
            case 0xb9: // Absolute,Y
            case 0xbd: // Absolute,X
                state.A = bus.read(effectiveAddress, true);
                instructor.setArithmeticFlags(state.A);
                break;


            /** CPY - Compare Y Register ********************************************/
            case 0xc0: // #Immediate
                instructor.cmp(state.Y, state.args[0]);
                break;
            case 0xc4: // Zero Page
            case 0xcc: // Absolute
                instructor.cmp(state.Y, bus.read(effectiveAddress, true));
                break;


            /** CMP - Compare Accumulator *******************************************/
            case 0xc9: // #Immediate
                instructor.cmp(state.A, state.args[0]);
                break;
            case 0xd2: // 65C02 CMP (ZP)
                if (IsLight
                    ) {
                    break;
                }

                goto case 0xc1;
            case 0xc1: // (Zero Page,X)
            case 0xc5: // Zero Page
            case 0xcd: // Absolute
            case 0xd1: // (Zero Page),Y
            case 0xd5: // Zero Page,X
            case 0xd9: // Absolute,Y
            case 0xdd: // Absolute,X
                instructor.cmp(state.A, bus.read(effectiveAddress, true));
                break;


            /** DEC - Decrement Memory **********************************************/
            case 0x3a: // 65C02 Immediate
                if (IsLight
                    ) {
                    break;
                }
                state.A = --state.A & 0xFF;
                instructor.setArithmeticFlags(state.A);
                break;
            case 0xc6: // Zero Page
            case 0xce: // Absolute
            case 0xd6: // Zero Page,X
            case 0xde: // Absolute,X
                tmp = (bus.read(effectiveAddress, true) - 1) & 0xff;
                bus.write(effectiveAddress, tmp);
                instructor.setArithmeticFlags(tmp);
                break;


            /** CPX - Compare X Register ********************************************/
            case 0xe0: // #Immediate
                instructor.cmp(state.X, state.args[0]);
                break;
            case 0xe4: // Zero Page
            case 0xec: // Absolute
                instructor.cmp(state.X, bus.read(effectiveAddress, true));
                break;


            /** SBC - Subtract with Carry (Borrow) **********************************/
            case 0xe9: // #Immediate
                if (state.decimalModeFlag) {
                    state.A = instructor.sbcDecimal(state.A, state.args[0]);
                } else {
                    state.A = instructor.sbc(state.A, state.args[0]);
                }
                break;
            case 0xf2: // 65C02 SBC (ZP)
                if (IsLight
                    ) {
                    break;
                }

                goto case 0xe1;
            case 0xe1: // (Zero Page,X)
            case 0xe5: // Zero Page
            case 0xed: // Absolute
            case 0xf1: // (Zero Page),Y
            case 0xf5: // Zero Page,X
            case 0xf9: // Absolute,Y
            case 0xfd: // Absolute,X
                if (state.decimalModeFlag) {
                    state.A = instructor.sbcDecimal(state.A, bus.read(effectiveAddress, true));
                } else {
                    state.A = instructor.sbc(state.A, bus.read(effectiveAddress, true));
                }
                break;


            /** INC - Increment Memory **********************************************/
            case 0x1a: // 65C02 Increment Immediate
                if (IsLight
                    ) {
                    break;
                }
                state.A = ++state.A & 0xff;
                instructor.setArithmeticFlags(state.A);
                break;
            case 0xe6: // Zero Page
            case 0xee: // Absolute
            case 0xf6: // Zero Page,X
            case 0xfe: // Absolute,X
                tmp = (bus.read(effectiveAddress, true) + 1) & 0xff;
                bus.write(effectiveAddress, tmp);
                instructor.setArithmeticFlags(tmp);
                break;


            /** 65C02 RMB - Reset Memory Bit **************************************/
            case 0x07: // 65C02 RMB0 - Zero Page
                if (IsLight
                    ) {
                    break;
                }
                tmp = bus.read(effectiveAddress, true) & 0xff;
                tmp &= ~(1 << 0);
                bus.write(effectiveAddress, tmp);
                break;
            case 0x17: // 65C02 RMB1 - Zero Page
                if (IsLight
                    ) {
                    break;
                }
                tmp = bus.read(effectiveAddress, true) & 0xff;
                tmp &= ~(1 << 1);
                bus.write(effectiveAddress, tmp);
                break;
            case 0x27: // 65C02 RMB2 - Zero Page
                if (IsLight
                    ) {
                    break;
                }
                tmp = bus.read(effectiveAddress, true) & 0xff;
                tmp &= ~(1 << 2);
                bus.write(effectiveAddress, tmp);
                break;
            case 0x37: // 65C02 RMB3 - Zero Page
                if (IsLight
                    ) {
                    break;
                }
                tmp = bus.read(effectiveAddress, true) & 0xff;
                tmp &= ~(1 << 3);
                bus.write(effectiveAddress, tmp);
                break;
            case 0x47: // 65C02 RMB4 - Zero Page
                if (IsLight
                    ) {
                    break;
                }
                tmp = bus.read(effectiveAddress, true) & 0xff;
                tmp &= ~(1 << 4);
                bus.write(effectiveAddress, tmp);
                break;
            case 0x57: // 65C02 RMB5 - Zero Page
                if (IsLight
                    ) {
                    break;
                }
                tmp = bus.read(effectiveAddress, true) & 0xff;
                tmp &= ~(1 << 5);
                bus.write(effectiveAddress, tmp);
                break;
            case 0x67: // 65C02 RMB6 - Zero Page
                if (IsLight
                    ) {
                    break;
                }
                tmp = bus.read(effectiveAddress, true) & 0xff;
                tmp &= ~(1 << 6);
                bus.write(effectiveAddress, tmp);
                break;
            case 0x77: // 65C02 RMB7 - Zero Page
                if (IsLight
                    ) {
                    break;
                }
                tmp = bus.read(effectiveAddress, true) & 0xff;
                tmp &= ~(1 << 7);
                bus.write(effectiveAddress, tmp);
                break;


            /** 65C02 SMB - Set Memory Bit **************************************/
            case 0x87: // 65C02 SMB0 - Zero Page
                if (IsLight
                    ) {
                    break;
                }
                tmp = bus.read(effectiveAddress, true) & 0xff;
                tmp |= (1);
                bus.write(effectiveAddress, tmp);
                break;
            case 0x97: // 65C02 SMB1 - Zero Page
                if (IsLight
                    ) {
                    break;
                }
                tmp = bus.read(effectiveAddress, true) & 0xff;
                tmp |= (1 << 1);
                bus.write(effectiveAddress, tmp);
                break;
            case 0xa7: // 65C02 SMB2 - Zero Page
                if (IsLight
                    ) {
                    break;
                }
                tmp = bus.read(effectiveAddress, true) & 0xff;
                tmp |= (1 << 2);
                bus.write(effectiveAddress, tmp);
                break;
            case 0xb7: // 65C02 SMB3 - Zero Page
                if (IsLight
                    ) {
                    break;
                }
                tmp = bus.read(effectiveAddress, true) & 0xff;
                tmp |= (1 << 3);
                bus.write(effectiveAddress, tmp);
                break;
            case 0xc7: // 65C02 SMB4 - Zero Page
                if (IsLight
                    ) {
                    break;
                }
                tmp = bus.read(effectiveAddress, true) & 0xff;
                tmp |= (1 << 4);
                bus.write(effectiveAddress, tmp);
                break;
            case 0xd7: // 65C02 SMB5 - Zero Page
                if (IsLight
                    ) {
                    break;
                }
                tmp = bus.read(effectiveAddress, true) & 0xff;
                tmp |= (1 << 5);
                bus.write(effectiveAddress, tmp);
                break;
            case 0xe7: // 65C02 SMB6 - Zero Page
                if (IsLight
                    ) {
                    break;
                }
                tmp = bus.read(effectiveAddress, true) & 0xff;
                tmp |= (1 << 6);
                bus.write(effectiveAddress, tmp);
                break;
            case 0xf7: // 65C02 SMB7 - Zero Page
                if (IsLight
                    ) {
                    break;
                }
                tmp = bus.read(effectiveAddress, true) & 0xff;
                tmp |= (1 << 7);
                bus.write(effectiveAddress, tmp);
                break;

            /** 65C02 TRB/TSB - Test and Reset Bit/Test and Set Bit ***************/
            case 0x14: // 65C02 TRB - Test and Reset bit - Zero Page
            case 0x1c: // 65C02 TRB - Test and Reset bit - Absolute
                if (IsLight
                    ) {
                    break;
                }
                tmp = bus.read(effectiveAddress, true);
                instructor.setZeroFlag((state.A & tmp) == 0);
                tmp = (tmp &= ~(state.A)) & 0xff;
                bus.write(effectiveAddress,tmp);
                break;

            case 0x04: // 65C02 TSB - Test and Set bit - Zero Page
            case 0x0c: // 65C02 TSB - Test and Set bit - Absolute
                if (IsLight
                    ) {
                    break;
                }
                tmp = bus.read(effectiveAddress, true);
                instructor.setZeroFlag((state.A & tmp) == 0);
                tmp = (tmp |= (state.A)) & 0xff;
                bus.write(effectiveAddress,tmp);
                break;

            /** 65C02 BBR - Branch if Bit Reset *************************************/
            case 0x0f: // 65C02 BBR - Branch if bit 0 reset - Zero Page
                if (IsLight
                    ) {
                    break;
                }
                tmp = bus.read(effectiveAddress, true);
                if ((tmp & 1 << 0) == 0) {
                    state.PC = relAddress(state.args[1]);
                }
                break;

            case 0x1f: // 65C02 BBR - Branch if bit 1 reset - Zero Page
                if (IsLight
                    ) {
                    break;
                }
                tmp = bus.read(effectiveAddress, true);
                if ((tmp & 1 << 1) == 0) {
                    state.PC = relAddress(state.args[1]);
                }
                break;

            case 0x2f: // 65C02 BBR - Branch if bit 2 reset - Zero Page
                if (IsLight
                    ) {
                    break;
                }
                tmp = bus.read(effectiveAddress, true);
                if ((tmp & 1 << 2) == 0) {
                    state.PC = relAddress(state.args[1]);
                }
                break;

            case 0x3f: // 65C02 BBR - Branch if bit 3 reset - Zero Page
                if (IsLight
                    ) {
                    break;
                }
                tmp = bus.read(effectiveAddress, true);
                if ((tmp & 1 << 3) == 0) {
                    state.PC = relAddress(state.args[1]);
                }
                break;

            case 0x4f: // 65C02 BBR - Branch if bit 4 reset - Zero Page
                if (IsLight
                    ) {
                    break;
                }
                tmp = bus.read(effectiveAddress, true);
                if ((tmp & 1 << 4) == 0) {
                    state.PC = relAddress(state.args[1]);
                }
                break;


            case 0x5f: // 65C02 BBR - Branch if bit 5 reset - Zero Page
                if (IsLight
                    ) {
                    break;
                }
                tmp = bus.read(effectiveAddress, true);
                if ((tmp & 1 << 5) == 0) {
                    state.PC = relAddress(state.args[1]);
                }
                break;

            case 0x6f: // 65C02 BBR - Branch if bit 6 reset - Zero Page
                if (IsLight
                    ) {
                    break;
                }
                tmp = bus.read(effectiveAddress, true);
                if ((tmp & 1 << 6) == 0) {
                    state.PC = relAddress(state.args[1]);
                }
                break;

            case 0x7f: // 65C02 BBR - Branch if bit 5 reset - Zero Page
                if (IsLight
                    ) {
                    break;
                }
                tmp = bus.read(effectiveAddress, true);
                if ((tmp & 1 << 7) == 0) {
                    state.PC = relAddress(state.args[1]);
                }
                break;


            /** 65C02 BBS - Branch if Bit Set  ************************************/
            case 0x8f: // 65C02 BBS - Branch if bit 0 set - Zero Page
                if (IsLight
                    ) {
                    break;
                }
                tmp = bus.read(effectiveAddress, true);
                if ((tmp & 1 << 0) > 0) {
                    state.PC = relAddress(state.args[1]);
                }
                break;

            case 0x9f: // 65C02 BBS - Branch if bit 1 set - Zero Page
                if (IsLight
                    ) {
                    break;
                }
                tmp = bus.read(effectiveAddress, true);
                if ((tmp & 1 << 1) > 0) {
                    state.PC = relAddress(state.args[1]);
                }
                break;

            case 0xaf: // 65C02 BBS - Branch if bit 2 set - Zero Page
                if (IsLight
                    ) {
                    break;
                }
                tmp = bus.read(effectiveAddress, true);
                if ((tmp & 1 << 2) > 0) {
                    state.PC = relAddress(state.args[1]);
                }
                break;

            case 0xbf: // 65C02 BBS - Branch if bit 3 set - Zero Page
                if (IsLight
                    ) {
                    break;
                }
                tmp = bus.read(effectiveAddress, true);
                if ((tmp & 1 << 3) > 0) {
                    state.PC = relAddress(state.args[1]);
                }
                break;


            case 0xcf: // 65C02 BBS - Branch if bit 4 set - Zero Page
                if (IsLight
                    ) {
                    break;
                }
                tmp = bus.read(effectiveAddress, true);
                if ((tmp & 1 << 4) > 0) {
                    state.PC = relAddress(state.args[1]);
                }
                break;


            case 0xdf: // 65C02 BBS - Branch if bit 5 set - Zero Page
                if (IsLight
                    ) {
                    break;
                }
                tmp = bus.read(effectiveAddress, true);
                if ((tmp & 1 << 5) > 0) {
                    state.PC = relAddress(state.args[1]);
                }
                break;

            case 0xef: // 65C02 BBS - Branch if bit 6 set - Zero Page
                if (IsLight
                    ) {
                    break;
                }
                tmp = bus.read(effectiveAddress, true);
                if ((tmp & 1 << 6) > 0) {
                    state.PC = relAddress(state.args[1]);
                }
                break;

            case 0xff: // 65C02 BBS - Branch if bit 5 set - Zero Page
                if (IsLight
                    ) {
                    break;
                }
                tmp = bus.read(effectiveAddress, true);
                if ((tmp & 1 << 7) > 0) {
                    state.PC = relAddress(state.args[1]);
                }
                break;


            /** Unimplemented Instructions ****************************************/
            // TODO: Create a flag to enable highly-accurate emulation of unimplemented instructions.
            default:
                opTrap = true;
                break;
        }

        delayLoop(state.IR);

        // Peek ahead to the next insturction and arguments
        peekAhead();
        }

        
        public void cycle2()
        {
            opBeginTime = DateTime.Now.Ticks;
            if (state.signalStop) return;
            state.lastPc = state.PC;

            
            if (state.nmiAsserted)
                debugger?.handleNmi();
            else if (state.irqAsserted && !IrqDisableFlag)
                debugger?.handleIrq(state.PC);


            // Fetch memory location for this instruction.
            state.IR = Bus.read(state.PC, true);
            var irAddressMode = (state.IR >> 2) & 0x07;  // Bits 3-5 of IR:  [ | | |X|X|X| | ]
            var irOpMode = state.IR & 0x03;              // Bits 6-7 of IR:  [ | | | | | |X|X]
            var oldPC = state.PC;
            incrementPC();
            opTrap = false;
            //if(state.PC == 0x40D)
            //state.PC = 0x0317;
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
                            if (((state.IR >> 5) & 1) == 0)
                            { // Zero Page
                                effectiveAddress = state.args[0];
                            }
                            else
                            { // Absolute
                                effectiveAddress = Memory.address(state.args[0], state.args[1]);
                            }
                            break;
                        case 7:
                            if (((state.IR >> 5) & 1) == 0) // Zero Page, X
                                effectiveAddress = zpxAddress(state.args[0]);
                            else // Absolute, X
                                effectiveAddress = xAddress(state.args[0], state.args[1]);
                            break;
                        case 0: // stk,S
                            effectiveAddress = state.args[0] + state.SP & 0xffff;
                            break;
                        case 4: // (stk,S),Y
                            effectiveAddress = state.args[0] + state.SP & 0xffff;
                            effectiveAddress = yAddress(Bus.read(effectiveAddress, true),
                                    Bus.read(effectiveAddress + 1, true));
                            break;
                        case 1: // r,R
                            effectiveAddress = state.args[0] + state.R & 0xffff;
                            break;
                        case 5:// (r,R),Y
                            effectiveAddress = state.args[0] + state.R & 0xffff;
                            effectiveAddress = yAddress(Bus.read(effectiveAddress, true),
                                    Bus.read(effectiveAddress + 1, true));
                            break;
                    }
                    break;
                case 1:
                    switch (irAddressMode)
                    {
                        case 0: // (Zero Page,X)
                            tmp = (state.args[0] + state.X) & 0xff;
                            effectiveAddress = Memory.address(Bus.read(tmp, true), Bus.read(tmp + 1, true));
                            break;
                        case 1: // Zero Page
                            effectiveAddress = state.args[0];
                            break;
                        case 2: // #Immediate
                            effectiveAddress = -1;
                            break;
                        case 3: // Absolute
                            effectiveAddress = Memory.address(state.args[0], state.args[1]);
                            break;
                        case 4: // (Zero Page),Y
                            tmp = Memory.address(Bus.read(state.args[0], true),
                                          Bus.read((state.args[0] + 1) & 0xff, true));
                            effectiveAddress = (tmp + state.Y) & 0xffff;
                            break;
                        case 5: // Zero Page,X
                            effectiveAddress = zpxAddress(state.args[0]);
                            break;
                        case 6: // Absolute, Y
                            effectiveAddress = yAddress(state.args[0], state.args[1]);
                            break;
                        case 7: // Absolute, X
                            effectiveAddress = xAddress(state.args[0], state.args[1]);
                            break;
                    }
                    break;
                default:
                    Log.er($"unk opmode {(Mode)irOpMode} - {irOpMode} - {irOpMode:X}");
                    break;
            }
            // Execute
            if (state.IR != 0x0) // IR-Opcode: 0x{state.IR:X2}, IR-Mode: {opcodeNames[state.IR]},
                Log.nf($"{state.ToTraceEvent()} {this.getProcessorStatusString()}");
            switch (state.IR)
            {

                /** Single Byte Instructions; Implied and Relative **/
                case 0x00: // BRK - Force Interrupt - Implied
                    debugger?.handleBrk(state.PC + 1);
                    break;
                case 0x08: // PHP - Push Processor Status - Implied
                    // Break flag is always set in the stack value.
                    stack.PushByte(state.getStatusFlag());
                    break;
                case 0x10: // BPL - Branch if Positive - Relative
                    if (!negativeFlag)
                        state.PC = relAddress(state.args[0]);
                    break;
                case 0x18: // CLC - Clear Carry Flag - Implied
                    carryFlag = false;
                    break;
                case 0x20: // JSR - Jump to Subroutine - Implied
                    stack.PushWord(state.PC - 1);
                    state.PC = Memory.address(state.args[0], state.args[1]);
                    break;
                case 0xfc: // JSR - (Absolute Indexed Indirect,X)
                    stack.PushWord(state.PC - 1);
                    tmp = (((state.args[1] << 8) | state.args[0]) + state.X) & 0xffff;
                    state.PC = Memory.address(Bus.read(tmp, true), Bus.read(tmp + 1, true));
                    break;
                case 0x28: // PLP - Pull Processor Status - Implied
                    ProcessorStatus = (stack.PopByte());
                    break;
                case 0x30: // BMI - Branch if Minus - Relative
                    if (negativeFlag)
                        state.PC = relAddress(state.args[0]);
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
                    stack.Push(state.A, false);
                    break;
                case 0x50: // BVC - Branch if Overflow Clear - Relative
                    if (!overFlowFlag)
                        state.PC = relAddress(state.args[0]);
                    break;
                case 0x58: // CLI - Clear Interrupt Disable - Implied
                    IrqDisableFlag = false;
                    break;
                case 0x5a: // 65C02 PHY - Push Y to stack
                    if (IsLight)
                        break;
                    stack.Push(state.Y, true);
                    break;
                case 0x60: // RTS - Return from Subroutine - Implied
                    lo = stack.PopByte();
                    hi = stack.PopByte();
                    setProgramCounter((Memory.address(lo, hi) + 1) & 0xffff);
                    break;
                case 0x68: // PLA - Pull Accumulator - Implied
                    state.A = stack.Pop(false);
                    instructor.setArithmeticFlags(state.A, false);
                    break;
                case 0x70: // BVS - Branch if Overflow Set - Relative
                    if (overFlowFlag)
                        state.PC = relAddress(state.args[0]);
                    break;
                case 0x78: // SEI - Set Interrupt Disable - Implied
                    IrqDisableFlag = true;
                    break;
                case 0x7a: // 65C02 PLY - Pull Y from Stack
                    if (IsLight)
                        break;
                    state.Y = stack.Pop(true);
                    instructor.setArithmeticFlags(state.Y, true);
                    break;
                case 0x80: // 65C02 BRA - Branch Always
                    if (IsLight)
                        state.PC = relAddress(state.args[0]);
                    break;
                case 0x88: // DEY - Decrement Y Register - Implied
                    state.Y = --state.Y & screen.maskXWidth();
                    instructor.setArithmeticFlags(state.Y, true);
                    break;
                case 0x8a: // TXA - Transfer X to Accumulator - Implied
                    state.A = state.X;
                    instructor.setArithmeticFlags(state.A, false);
                    break;
                case 0x90: // BCC - Branch if Carry Clear - Relative
                    if (!carryFlag)
                    {
                        state.PC = relAddress(state.args[0]);
                    }

                    break;
                case 0x98: // TYA - Transfer Y to Accumulator - Implied
                    state.A = state.Y;
                    instructor.setArithmeticFlags(state.A, false);
                    break;
                case 0x9a: // TXS - Transfer X to Stack Pointer - Implied
                    if (state.indexWidthFlag)
                        state.SP = (state.SP & 0xff00 | (state.X & 0xff));
                    else
                        state.SP = (state.X);
                    break;
                case 0xa8: // TAY - Transfer Accumulator to Y - Implied
                    state.Y = state.A;
                    instructor.setArithmeticFlags(state.Y, true);
                    break;
                case 0xaa: // TAX - Transfer Accumulator to X - Implied
                    state.X = state.A;
                    instructor.setArithmeticFlags(state.X, true);
                    break;
                case 0x9b: // TXY - Transfer X to Y
                    state.Y = state.X;
                    instructor.setArithmeticFlags(state.Y, true);
                    break;
                case 0xbb: // TYX - Transfer Y to X
                    state.X = state.Y;
                    instructor.setArithmeticFlags(state.X, true);
                    break;
                case 0xb0: // BCS - Branch if Carry Set - Relative
                    if (carryFlag)
                        state.PC = relAddress(state.args[0]);
                    break;
                case 0xb8: // CLV - Clear Overflow Flag - Implied
                    overFlowFlag = false;
                    break;
                case 0xba: // TSX - Transfer Stack Pointer to X - Implied
                    state.X = state.SP;
                    instructor.setArithmeticFlags(state.X, true);
                    break;
                case 0xc8: // INY - Increment Y Register - Implied
                    state.Y = ++state.Y & screen.maskXWidth();
                    instructor.setArithmeticFlags(state.Y, true);
                    break;
                case 0xca: // DEX - Decrement X Register - Implied
                    state.X = --state.X & screen.maskXWidth();
                    instructor.setArithmeticFlags(state.X, true);
                    break;
                case 0xd0: // BNE - Branch if Not Equal to Zero - Relative
                    if (!zeroFlag)
                        state.PC = relAddress(state.args[0]);
                    break;
                case 0xd8: // CLD - Clear Decimal Mode - Implied
                    DecimalModeFlag = false;
                    break;
                case 0xda: // 65C02 PHX - Push X to stack
                    if (IsLight)
                        break;
                    stack.Push(state.X, true);
                    break;
                case 0xe8: // INX - Increment X Register - Implied
                    state.X = ++state.X & screen.maskXWidth();
                    instructor.setArithmeticFlags(state.X, true);
                    break;
                case 0xea: // NOP
                    // Do nothing.
                    break;
                case 0xf0: // BEQ - Branch if Equal to Zero - Relative
                    //! Fucking zero-based-flag, stops cyclical jump
                    if (zeroFlag)
                        state.PC = relAddress(state.args[0] - 10);
                    break;
                case 0xf8: // SED - Set Decimal Flag - Implied
                    DecimalModeFlag = true;
                    break;
                case 0xfa: // 65C02 PLX - Pull X from Stack
                    if (IsLight)
                        break;
                    state.X = stack.Pop(true);
                    instructor.setArithmeticFlags(state.X, true);
                    break;
                case 0x62: // PER
                    stack.PushWord(state.args[0] + state.PC);
                    break;
                case 0xd4: // PEI
                    stack.PushWord(
                        Bus.read(state.args[0], true) | (Bus.read(state.args[0] + 1, true) << 8));
                    break;
                case 0xf4: // PEA
                    stack.PushWord(Memory.address(state.args[0], state.args[1]));
                    break;
                case 0xeb: // XBA - Exchange A bytes
                    if (state.mWidthFlag)
                    {
                        tmp = state.A_TOP >> 8;
                        state.A_TOP = state.A << 8;
                        state.A = tmp;
                    }
                    else
                        state.A = ((state.A & 0xff) << 8) | ((state.A >> 8) & 0xff);
                    break;
                case 0xfb: // XCE - Exchange Carry and Emulation flags
                    bool oldCarry = state.carryFlag;
                    state.carryFlag = state.emulationFlag;
                    state.emulationFlag = oldCarry;
                    if (oldCarry)
                    {
                        if (!state.mWidthFlag)
                            state.A_TOP = state.A & 0xff00;
                        state.mWidthFlag = state.indexWidthFlag = true;
                        state.A &= 0xff;
                        state.X &= 0xff;
                        state.Y &= 0xff;
                    }
                    break;
                case 0xc2: // REP - Reset status bits
                    ProcessorStatus = (int)(ProcessorStatus & (state.args[0] ^ 0xffffffff));
                    break;
                case 0xe2: // SEP - Set status bits
                    ProcessorStatus = (ProcessorStatus | state.args[0]);
                    break;
                case 0xdb: // STP
                    state.signalStop = true;
                    break;
                case 0xcb: // WAI
                    state.intWait = true;
                    break;
                case 0x22: // ENT - Enter word, RHI, I=PC+2, PC=(PC)
                    stack.RPushWord(state.I);
                    state.I = state.PC + 2;
                    state.PC = readMemory(state.PC, false);
                    break;
                case 0x42: // NXA - Next word into A, A=(I), I=I+1/I=I+2
                    state.A = readMemory(state.I, false);
                    state.I += state.mWidthFlag ? 1 : 2;
                    break;
                case 0x02: // NXT - Next word, PC=(I), I=I+2
                    state.PC = readMemory(state.I, false);
                    state.I += 2;
                    break;
                case 0x8b: // TXR - Transfer X to R
                    if (state.indexWidthFlag)
                    {
                        state.R = (state.R & 0xff00) | (state.X & 0xff);
                    }
                    else
                    {
                        state.R = state.X;
                    }

                    instructor.setArithmeticFlags(state.R, true);
                    break;
                case 0xab: // TRX - Transfer R to X
                    state.X = state.R;
                    instructor.setArithmeticFlags(state.X, true);
                    break;
                case 0x5c: // TXI - Transfer X to I
                    state.I = state.X;
                    instructor.setArithmeticFlags(state.X, true);
                    break;
                case 0xdc: // TIX - Transfer I to X
                    state.X = state.I;
                    instructor.setArithmeticFlags(state.X, true);
                    break;
                case 0x4b: // RHA - Push accumulator to R stack
                    stack.RPush(state.A, false);
                    break;
                case 0x6b: // RLA - Pull accumulator from R stack
                    state.A = stack.RPop(false);
                    instructor.setArithmeticFlags(state.A, false);
                    break;
                case 0x1b: // RHX - Push X register to R stack
                    stack.RPush(state.X, true);
                    break;
                case 0x3b: // RLX - Pull X register from R stack
                    state.X = stack.RPop(true);
                    instructor.setArithmeticFlags(state.X, true);
                    break;
                case 0x5b: // RHY - Push Y register to R stack
                    stack.RPush(state.Y, true);
                    break;
                case 0x7b: // RLY - Pull Y register from R stack
                    state.Y = stack.RPop(true);
                    instructor.setArithmeticFlags(state.Y, true);
                    break;
                case 0x0b: // RHI - Push I register to R stack
                    stack.RPushWord(state.I);
                    break;
                case 0x2b: // RLI - Pull I register from R stack
                    state.I = stack.RPopWord();
                    instructor.setArithmeticFlags(state.I, true);
                    break;
                case 0x82: // RER - Push effective relative address to R stack
                    stack.RPushWord(state.PC + state.args[0]);
                    break;

                // Undocumented. See http://bigfootinformatika.hu/65el02/archive/65el02_instructions.txt
                case 0x44: // REA - push address to R stack
                    stack.RPushWord(Memory.address(state.args[0], state.args[1]));
                    break;
                case 0x54: // REI - push indirect zp address to R stack
                    stack.RPushWord(readWord(state.args[0]));
                    break;

                /** JMP *****************************************************************/
                case 0x4c: // JMP - Absolute
                    state.PC = Memory.address(state.args[0], state.args[1]);
                    break;
                case 0x6c: // JMP - Indirect
                    lo = Memory.address(state.args[0], state.args[1]); // Address of low byte
                    if (state.args[0] == 0xff && IsLight)
                        hi = Memory.address(0x00, state.args[1]);
                    else
                        hi = lo + 1;
                    state.PC = Memory.address(Bus.read(lo, true), Bus.read(hi, true));
                    break;
                case 0x7c: // 65C02 JMP - (Absolute Indexed Indirect,X)
                    if (IsLight)
                        break;
                    lo = (((state.args[1] << 8) | state.args[0]) + state.X) & 0xffff;
                    hi = lo + 1;
                    state.PC = Memory.address(Bus.read(lo, true), Bus.read(hi, true));
                    break;

                /** ORA - Logical Inclusive Or ******************************************/
                case 0x09: // #Immediate
                    state.A |= immediateArgs(false);
                    instructor.setArithmeticFlags(state.A, false);
                    break;
                case 0x12: // 65C02 ORA (ZP)
                    if (IsLight)
                        break;
                    goto case 0x01; // bridge
                case 0x01: // (Zero Page,X)
                case 0x05: // Zero Page
                case 0x0d: // Absolute
                case 0x11: // (Zero Page),Y
                case 0x15: // Zero Page,X
                case 0x19: // Absolute,Y
                case 0x1d: // Absolute,X
                case 0x03: // stk,S
                case 0x13: // (stk,S),Y
                    state.A |= readMemory(effectiveAddress, false);
                    instructor.setArithmeticFlags(state.A, false);
                    break;
                case 0x07: // r,R
                    if (IsLight)
                        break;
                    tmp = Bus.read(effectiveAddress, true) & 0xff;
                    tmp &= ~(1 << 0);
                    Bus.write(effectiveAddress, tmp);
                    break;
                case 0x17: // (r,R),Y
                    if (IsLight)
                        break;
                    tmp = Bus.read(effectiveAddress, true) & 0xff;
                    tmp &= ~(1 << 1);
                    Bus.write(effectiveAddress, tmp);
                    break;
                /** ASL - Arithmetic Shift Left *****************************************/
                case 0x0a: // Accumulator
                    state.A = instructor.asl(state.A);
                    instructor.setArithmeticFlags(state.A, false);
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
                    instructor.setZeroFlag((state.A & immediateArgs(false)) == 0);
                    break;
                case 0x34: // 65C02 Zero Page,X
                    if (IsLight)
                        break;
                    goto case 0x24;
                case 0x24: // Zero Page
                case 0x2c: // Absolute
                case 0x3c: // Absolute,X
                    tmp = readMemory(effectiveAddress, false);
                    instructor.setZeroFlag((state.A & tmp) == 0);
                    instructor.setNegativeFlag((tmp & screen.negativeMWidth()) != 0);
                    instructor.setOverflowFlag((tmp & (state.mWidthFlag ? 0x40 : 0x4000)) != 0);
                    break;


                /** AND - Logical AND ***************************************************/
                case 0x29: // #Immediate
                    state.A &= state.args[0];//immediateArgs(false);
                    instructor.setArithmeticFlags(state.A);
                    break;
                case 0x32: // 65C02 AND (ZP)
                    if (IsLight)
                        break;
                    goto case 0x21;
                case 0x21: // (Zero Page,X)
                case 0x25: // Zero Page
                case 0x2d: // Absolute
                case 0x31: // (Zero Page),Y
                case 0x35: // Zero Page,X
                case 0x39: // Absolute,Y
                case 0x3d: // Absolute,X
                case 0x23: // stk,S
                case 0x33: // (stk,S),Y
                    state.A &= readMemory(effectiveAddress, false);
                    instructor.setArithmeticFlags(state.A, false);
                    break;
                case 0x27: // r,R
                    if (IsLight)
                        break;
                    tmp = Bus.read(effectiveAddress, true) & 0xff;
                    tmp &= ~(1 << 2);
                    Bus.write(effectiveAddress, tmp);
                    break;
                case 0x37: // (r,R),Y
                    if (IsLight)
                        break;
                    tmp = Bus.read(effectiveAddress, true) & 0xff;
                    tmp &= ~(1 << 3);
                    Bus.write(effectiveAddress, tmp);
                    break;


                /** ROL - Rotate Left ***************************************************/
                case 0x2a: // Accumulator
                    state.A = instructor.rol(state.A);
                    instructor.setArithmeticFlags(state.A, false);
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
                    state.A ^= immediateArgs(false);
                    instructor.setArithmeticFlags(state.A, false);
                    break;
                case 0x52: // 65C02 EOR (ZP)
                    if (IsLight)
                        break;
                    goto case 0x41;
                case 0x41: // (Zero Page,X)
                case 0x45: // Zero Page
                case 0x4d: // Absolute
                case 0x51: // (Zero Page,Y)
                case 0x55: // Zero Page,X
                case 0x59: // Absolute,Y
                case 0x5d: // Absolute,X
                case 0x43: // stk,S
                case 0x53: // (stk,S),Y
                    state.A ^= readMemory(effectiveAddress, false);
                    instructor.setArithmeticFlags(state.A, false);
                    break;
                case 0x47: // r,R
                    if (IsLight)
                        break;
                    tmp = Bus.read(effectiveAddress, true) & 0xff;
                    tmp &= ~(1 << 4);
                    Bus.write(effectiveAddress, tmp);
                    break;
                case 0x57: // (r,R),Y
                    if (IsLight)
                        break;
                    tmp = Bus.read(effectiveAddress, true) & 0xff;
                    tmp &= ~(1 << 5);
                    Bus.write(effectiveAddress, tmp);
                    break;

                /** LSR - Logical Shift Right *******************************************/
                case 0x4a: // Accumulator
                    state.A = instructor.lsr(state.A);
                    instructor.setArithmeticFlags(state.A, false);
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
                    state.A =
                        state.decimalModeFlag ?
                            instructor.ADCDecimal(
                                state.A, immediateArgs(false)) :
                            instructor.ADC(state.A, immediateArgs(false));

                    break;
                case 0x72: // 65C02 ADC (ZP)
                    if (IsLight)
                        break;
                    goto case 0x61;
                case 0x61: // (Zero Page,X)
                case 0x65: // Zero Page
                case 0x6d: // Absolute
                case 0x71: // (Zero Page),Y
                case 0x75: // Zero Page,X
                case 0x79: // Absolute,Y
                case 0x7d: // Absolute,X
                case 0x63: // stk,S
                case 0x73: // (stk,S),Y
                    state.A =
                        state.decimalModeFlag ?
                            instructor.ADCDecimal(state.A, readMemory(effectiveAddress, false)) :
                            instructor.ADC(state.A, readMemory(effectiveAddress, false));
                    break;
                case 0x67: // r,R
                    if (IsLight)
                        break;
                    tmp = Bus.read(effectiveAddress, true) & 0xff;
                    tmp &= ~(1 << 6);
                    Bus.write(effectiveAddress, tmp);
                    break;
                case 0x77: // (r,R),Y
                    if (IsLight)
                        break;
                    tmp = Bus.read(effectiveAddress, true) & 0xff;
                    tmp &= ~(1 << 7);
                    Bus.write(effectiveAddress, tmp);
                    break;
                /** ROR - Rotate Right **************************************************/
                case 0x6a: // Accumulator
                    state.A = instructor.ror(state.A);
                    instructor.setArithmeticFlags(state.A, false);
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
                    if (IsLight)
                        break;
                    goto case 0x81;
                case 0x81: // (Zero Page,X)
                case 0x85: // Zero Page
                case 0x8d: // Absolute
                case 0x91: // (Zero Page),Y
                case 0x95: // Zero Page,X
                case 0x99: // Absolute,Y
                case 0x9d: // Absolute,X
                case 0x83: // stk,S
                case 0x93: // (stk,S),Y
                    writeMemory(effectiveAddress, state.A, false);
                    break;
                case 0x87: // r,R
                    if (IsLight)
                        break;
                    tmp = Bus.read(effectiveAddress, true) & 0xff;
                    tmp |= (1);
                    Bus.write(effectiveAddress, tmp);
                    break;
                case 0x97: // (r,R),Y
                    if (IsLight)
                        break;
                    tmp = Bus.read(effectiveAddress, true) & 0xff;
                    tmp |= (1 << 1);
                    Bus.write(effectiveAddress, tmp);
                    break;


                /** STY - Store Y Register **********************************************/
                case 0x84: // Zero Page
                case 0x8c: // Absolute
                case 0x94: // Zero Page,X
                    writeMemory(effectiveAddress, state.Y, true);
                    break;


                /** STX - Store X Register **********************************************/
                case 0x86: // Zero Page
                case 0x8e: // Absolute
                case 0x96: // Zero Page,Y
                    writeMemory(effectiveAddress, state.X, true);
                    break;

                /** STZ - 65C02 Store Zero ****************************************************/
                case 0x64: // Zero Page
                case 0x74: // Zero Page,X
                case 0x9c: // Absolute
                case 0x9e: // Absolute,X
                    if (IsLight)
                        break;
                    writeMemory(effectiveAddress, 0, false);
                    break;

                /** LDY - Load Y Register ***********************************************/
                case 0xa0: // #Immediate
                    state.Y = immediateArgs(true);
                    instructor.setArithmeticFlags(state.Y, true);
                    break;
                case 0xa4: // Zero Page
                case 0xac: // Absolute
                case 0xb4: // Zero Page,X
                case 0xbc: // Absolute,X
                    state.Y = readMemory(effectiveAddress, true);
                    instructor.setArithmeticFlags(state.Y, true);
                    break;


                /** LDX - Load X Register ***********************************************/
                case 0xa2: // #Immediate
                    state.X = state.args[0];//immediateArgs(true);
                    instructor.setArithmeticFlags(state.X, true);
                    break;
                case 0xa6: // Zero Page
                case 0xae: // Absolute
                case 0xb6: // Zero Page,Y
                case 0xbe: // Absolute,Y
                    state.X = readMemory(effectiveAddress, true);
                    instructor.setArithmeticFlags(state.X, true);
                    break;


                /** LDA - Load Accumulator **********************************************/
                case 0xa9: // #Immediate
                    state.A = immediateArgs(false);
                    instructor.setArithmeticFlags(state.A, false);
                    break;
                case 0xb2: // 65C02 LDA (ZP)
                    if (IsLight)
                        break;
                    goto case 0xa1;
                case 0xa1: // (Zero Page,X)
                case 0xa5: // Zero Page
                case 0xad: // Absolute
                case 0xb1: // (Zero Page),Y
                case 0xb5: // Zero Page,X
                case 0xb9: // Absolute,Y
                case 0xbd: // Absolute,X
                case 0xa3: // stk,S
                case 0xb3: // (stk,S),Y
                    state.A = Bus.read(effectiveAddress, true);//readMemory(effectiveAddress, false);
                    if (state.A == 0x0)
                        Log.er($"effAdr: 0x{effectiveAddress:X}");
                    instructor.setArithmeticFlags(state.A);
                    break;
                case 0xa7: // r,R
                    if (IsLight) break;
                    tmp = Bus.read(effectiveAddress, true) & 0xff;
                    tmp |= (1 << 2);
                    Bus.write(effectiveAddress, tmp);
                    break;
                case 0xb7: // (r,R),Y
                    if (IsLight) break;
                    tmp = Bus.read(effectiveAddress, true) & 0xff;
                    tmp |= (1 << 3);
                    Bus.write(effectiveAddress, tmp);
                    break;

                /** CPY - Compare Y Register ********************************************/
                case 0xc0: // #Immediate
                    instructor.cmp(state.Y, immediateArgs(true), true);
                    break;
                case 0xc4: // Zero Page
                case 0xcc: // Absolute
                    instructor.cmp(state.Y, readMemory(effectiveAddress, true), true);
                    break;


                /** CMP - Compare Accumulator *******************************************/
                case 0xc9: // #Immediate
                    instructor.cmp(state.A, immediateArgs(false), false);
                    break;
                case 0xd2: // 65C02 CMP (ZP)
                    if (IsLight)
                        break;
                    goto case 0xc1;
                case 0xc1: // (Zero Page,X)
                case 0xc5: // Zero Page
                case 0xcd: // Absolute
                case 0xd1: // (Zero Page),Y
                case 0xd5: // Zero Page,X
                case 0xd9: // Absolute,Y
                case 0xdd: // Absolute,X
                case 0xc3: // stk,S
                case 0xd3: // (stk,S),Y
                    instructor.cmp(state.A, readMemory(effectiveAddress, false), false);
                    break;


                /** DEC - Decrement Memory **********************************************/
                case 0x3a: // 65C02 Immediate
                    if (IsLight)
                        break;
                    state.A = --state.A & screen.maskMWidth();
                    instructor.setArithmeticFlags(state.A, false);
                    break;
                case 0xc6: // Zero Page
                case 0xce: // Absolute
                case 0xd6: // Zero Page,X
                case 0xde: // Absolute,X
                    tmp = (readMemory(effectiveAddress, false) - 1) & screen.maskMWidth();
                    writeMemory(effectiveAddress, tmp, false);
                    instructor.setArithmeticFlags(tmp, false);
                    break;


                /** CPX - Compare X Register ********************************************/
                case 0xe0: // #Immediate
                    instructor.cmp(state.X, immediateArgs(true), true);
                    break;
                case 0xe4: // Zero Page
                case 0xec: // Absolute
                    instructor.cmp(state.X, readMemory(effectiveAddress, true), true);
                    break;


                /** SBC - Subtract with Carry (Borrow) **********************************/
                case 0xe9: // #Immediate
                    if (state.decimalModeFlag)
                    {
                        state.A = instructor.sbcDecimal(state.A, immediateArgs(false));
                    }
                    else
                    {
                        state.A = instructor.sbc(state.A, immediateArgs(false));
                    }

                    break;
                case 0xf2: // 65C02 SBC (ZP)
                    if (IsLight)
                        break;
                    goto case 0xe1;
                case 0xe1: // (Zero Page,X)
                case 0xe5: // Zero Page
                case 0xed: // Absolute
                case 0xf1: // (Zero Page),Y
                case 0xf5: // Zero Page,X
                case 0xf9: // Absolute,Y
                case 0xfd: // Absolute,X
                case 0xe3: // stk,S
                case 0xf3: // (stk,S),Y
                    if (state.decimalModeFlag)
                    {
                        state.A = instructor.sbcDecimal(state.A, readMemory(effectiveAddress, false));
                    }
                    else
                    {
                        state.A = instructor.sbc(state.A, readMemory(effectiveAddress, false));
                    }

                    break;
                //case 0xf7: // 65C02 SMB7 - Zero Page or (r,R),Y
                //    tmp = Bus.read(effectiveAddress, true) & 0xff;
                //    tmp |= (1 << 7);
                //    Bus.write(effectiveAddress, tmp);
                //    break;
                /** INC - Increment Memory **********************************************/
                case 0x1a: // 65C02 Increment Immediate
                    if (IsLight)
                        break;
                    state.A = ++state.A & screen.maskMWidth();
                    instructor.setArithmeticFlags(state.A, false);
                    break;
                case 0xe6: // Zero Page
                case 0xee: // Absolute
                case 0xf6: // Zero Page,X
                case 0xfe: // Absolute,X
                    tmp = (readMemory(effectiveAddress, false) + 1) & screen.maskMWidth();
                    writeMemory(effectiveAddress, tmp, false);
                    instructor.setArithmeticFlags(tmp, false);
                    break;
                case 0xc7: // 65C02 SMB4 - Zero Page
                    if (IsLight
                        )
                    {
                        break;
                    }
                    tmp = Bus.read(effectiveAddress, true) & 0xff;
                    tmp |= (1 << 4);
                    Bus.write(effectiveAddress, tmp);
                    break;
                case 0xd7: // 65C02 SMB5 - Zero Page
                    if (IsLight
                        )
                    {
                        break;
                    }
                    tmp = Bus.read(effectiveAddress, true) & 0xff;
                    tmp |= (1 << 5);
                    Bus.write(effectiveAddress, tmp);
                    break;
                case 0xe7: // 65C02 SMB6 - Zero Page
                    if (IsLight
                        )
                    {
                        break;
                    }
                    tmp = Bus.read(effectiveAddress, true) & 0xff;
                    tmp |= (1 << 6);
                    Bus.write(effectiveAddress, tmp);
                    break;
                case 0xf7: // 65C02 SMB7 - Zero Page
                    if (IsLight
                        )
                    {
                        break;
                    }
                    tmp = Bus.read(effectiveAddress, true) & 0xff;
                    tmp |= (1 << 7);
                    Bus.write(effectiveAddress, tmp);
                    break;

                /** 65C02 TRB/TSB - Test and Reset Bit/Test and Set Bit ***************/
                case 0x14: // 65C02 TRB - Test and Reset bit - Zero Page
                case 0x1c: // 65C02 TRB - Test and Reset bit - Absolute
                    if (IsLight)
                        break;
                    tmp = Bus.read(effectiveAddress, true);
                    instructor.setZeroFlag((state.A & tmp) == 0);
                    tmp = (tmp &= ~(state.A)) & 0xff;
                    Bus.write(effectiveAddress, tmp);
                    break;

                case 0x04: // 65C02 TSB - Test and Set bit - Zero Page
                case 0x0c: // 65C02 TSB - Test and Set bit - Absolute
                    if (IsLight)
                        break;
                    tmp = Bus.read(effectiveAddress, true);
                    instructor.setZeroFlag((state.A & tmp) == 0);
                    tmp = (tmp |= (state.A)) & 0xff;
                    Bus.write(effectiveAddress, tmp);
                    break;

                /** 65C02 BBR - Branch if Bit Reset *************************************/
                case 0x0f: // 65C02 BBR - Branch if bit 0 reset - Zero Page
                    if (IsLight)
                        break;
                    tmp = Bus.read(effectiveAddress, true);
                    if ((tmp & 1 << 0) == 0)
                    {
                        state.PC = relAddress(state.args[1]);
                    }
                    break;

                case 0x1f: // 65C02 BBR - Branch if bit 1 reset - Zero Page
                    if (IsLight)
                        break;
                    tmp = Bus.read(effectiveAddress, true);
                    if ((tmp & 1 << 1) == 0)
                    {
                        state.PC = relAddress(state.args[1]);
                    }
                    break;

                case 0x2f: // 65C02 BBR - Branch if bit 2 reset - Zero Page
                    if (IsLight)
                        break;
                    tmp = Bus.read(effectiveAddress, true);
                    if ((tmp & 1 << 2) == 0)
                    {
                        state.PC = relAddress(state.args[1]);
                    }
                    break;

                case 0x3f: // 65C02 BBR - Branch if bit 3 reset - Zero Page
                    if (IsLight)
                        break;
                    tmp = Bus.read(effectiveAddress, true);
                    if ((tmp & 1 << 3) == 0)
                    {
                        state.PC = relAddress(state.args[1]);
                    }
                    break;

                case 0x4f: // 65C02 BBR - Branch if bit 4 reset - Zero Page
                    if (IsLight)
                        break;
                    tmp = Bus.read(effectiveAddress, true);
                    if ((tmp & 1 << 4) == 0)
                    {
                        state.PC = relAddress(state.args[1]);
                    }
                    break;


                case 0x5f: // 65C02 BBR - Branch if bit 5 reset - Zero Page
                    if (IsLight)
                        break;
                    tmp = Bus.read(effectiveAddress, true);
                    if ((tmp & 1 << 5) == 0)
                    {
                        state.PC = relAddress(state.args[1]);
                    }
                    break;

                case 0x6f: // 65C02 BBR - Branch if bit 6 reset - Zero Page
                    if (IsLight)
                        break;
                    tmp = Bus.read(effectiveAddress, true);
                    if ((tmp & 1 << 6) == 0)
                    {
                        state.PC = relAddress(state.args[1]);
                    }
                    break;

                case 0x7f: // 65C02 BBR - Branch if bit 5 reset - Zero Page
                    if (IsLight)
                        break;
                    tmp = Bus.read(effectiveAddress, true);
                    if ((tmp & 1 << 7) == 0)
                    {
                        state.PC = relAddress(state.args[1]);
                    }
                    break;


                /** 65C02 BBS - Branch if Bit Set  ************************************/
                case 0x8f: // 65C02 BBS - Branch if bit 0 set - Zero Page
                    if (IsLight)
                        break;
                    tmp = Bus.read(effectiveAddress, true);
                    if ((tmp & 1 << 0) > 0)
                    {
                        state.PC = relAddress(state.args[1]);
                    }
                    break;

                case 0x9f: // 65C02 BBS - Branch if bit 1 set - Zero Page
                    if (IsLight)
                        break;
                    tmp = Bus.read(effectiveAddress, true);
                    if ((tmp & 1 << 1) > 0)
                    {
                        state.PC = relAddress(state.args[1]);
                    }
                    break;

                case 0xaf: // 65C02 BBS - Branch if bit 2 set - Zero Page
                    if (IsLight)
                        break;
                    tmp = Bus.read(effectiveAddress, true);
                    if ((tmp & 1 << 2) > 0)
                    {
                        state.PC = relAddress(state.args[1]);
                    }
                    break;

                case 0xbf: // 65C02 BBS - Branch if bit 3 set - Zero Page
                    if (IsLight)
                        break;
                    tmp = Bus.read(effectiveAddress, true);
                    if ((tmp & 1 << 3) > 0)
                        state.PC = relAddress(state.args[1]);
                    break;


                case 0xcf: // 65C02 BBS - Branch if bit 4 set - Zero Page
                    if (IsLight)
                        break;
                    tmp = Bus.read(effectiveAddress, true);
                    if ((tmp & 1 << 4) > 0)
                        state.PC = relAddress(state.args[1]);
                    break;


                case 0xdf: // 65C02 BBS - Branch if bit 5 set - Zero Page
                    if (IsLight)
                        break;
                    tmp = Bus.read(effectiveAddress, true);
                    if ((tmp & 1 << 5) > 0)
                        state.PC = relAddress(state.args[1]);
                    break;

                case 0xef: // 65C02 BBS - Branch if bit 6 set - Zero Page
                    if(IsLight)
                        break;
                    tmp = Bus.read(effectiveAddress, true);
                    if ((tmp & 1 << 6) > 0)
                        state.PC = relAddress(state.args[1]);
                    break;

                case 0xff: // 65C02 BBS - Branch if bit 5 set - Zero Page
                    if (IsLight)
                        break;
                    tmp = Bus.read(effectiveAddress, true);
                    if ((tmp & 1 << 7) > 0)
                        state.PC = relAddress(state.args[1]);
                    break;
                /** Unimplemented Instructions ****************************************/
                default:
                    opTrap = true;
                    break;
            }
            delayLoop(state.IR);

            peekAhead();
        }
    }
}
﻿namespace vm.components
{
    using System;
    using System.Drawing;
    using System.Linq;
    using cpu;
    using cpu.tables;

    public class ClockSpeed
    {
        public string Value { get; private set; } = "0Hz";
        public int this [string value]
        {
            get
            {
                Value = value;
                switch (value)
                {
                    case "90Hz": return 8000;
                    case "200Hz": return 4000;
                    case "500Hz": return 2000;
                    case "1MHz": return 1000;
                    case "2MHz": return 500;
                    case "3MHz": return 333;
                    case "4MHz": return 250;
                    case "5MHz": return 200;
                    case "6MHz": return 167;
                    case "7MHz": return 143;
                    case "8MHz": return 125;
                }
                return 0;
            }
        }

        public override string ToString() => Value;
    }
    public abstract class MonoCore : RegisterTable
    {
        protected static readonly ClockSpeed CLOCK_SPEED = new ClockSpeed();
        public bool StackBug { get; set; } = true;

        public Bus Bus { get; set; }
        public Action<string> logCallback { get; set; }

        public readonly CpuState state = new CpuState();


        public void RESET()
        {
            Log.wr("<<******CPU*RESET*****>>");
            Log.nf(this.state.ToTraceEvent());
            state.SP = StackBug ? S_STACK_TOP : S_STACK_TOP - 1;
            state.R = R_STACK_TOP;

            // Set program counter to the power-on-reset address
            // Default = 0x400
            state.PC = state.POR;

            // Clear instruction register.
            state.IR = 0;

            // Clear status register bits.
            state.carryFlag = false;
            state.zeroFlag = false;
            state.irqDisableFlag = false;
            state.decimalModeFlag = false;
            state.breakFlag = false;
            state.overflowFlag = false;
            state.negativeFlag = false;
            state.emulationFlag = true;
            state.mWidthFlag = true;
            state.indexWidthFlag = true;

            state.irqAsserted = false;

            state.signalStop = false;

            // Clear illegal opcode trap.
            state.opTrap = false;

            // Reset step counter
            state.stepCounter = 0L;

            // Reset registers.
            state.A = 0;
            state.X = 0;
            state.Y = 0;

            state.POR = 0x2000;
            // Default BRK address
            state.BRK = 0x2000;

            peekAhead();
        }
        
        public string getInstructionByteStatus()
        {
            switch (instructionSizes[state.IR])
            {
                case 0:
                case 1:
                    return $"0x{state.lastPc:X}  " +
                            $"0x{state.IR:X}      ";
                case 2:
                    return $"0x{state.lastPc:X}  " +
                            $"0x{state.IR:X} " +
                            $"0x{state.args[0]:X}   ";
                case 3:
                    return $"0x{state.lastPc:X}  " +
                           $"0x{state.IR:X} " +
                           $"0x{state.args[0]:X} " +
                           $"0x{state.args[1]:X}";
                default:
                    return null;
            }
        }


        


        protected void peekAhead()
        {
            state.nextIr = Bus.read(state.PC, true);
            var nextInstSize = state.getInstructionSize(state.nextIr);
            for (var i = 1; i < nextInstSize; i++)
            {
                var nextRead = (state.PC + i) & 0xffff;
                state.nextArgs[i - 1] = Bus.read(nextRead, true);
            }
        }
    }
}
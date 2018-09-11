namespace vm.components
{
    using System.Drawing;
    using cpu;
    using cpu.tables;
    using exceptions;
    using RC.Framework.Screens;

    public class Debugger : Component
    {
        private readonly bool _isLog;
        public Debugger(CPU c, bool isLog = false) : base(c)
        {
            _isLog = isLog;
        }


        public void handleNmi()
        {
            ou($"nmi break;");
            handleInterrupt(getState().PC, RegisterTable.NMI_VECTOR_L, RegisterTable.NMI_VECTOR_H, false);
            getCPU().NMI = false;
        }
        public void handleIrq(int returnPc)
        {
            ou($"irq break;");
            handleInterrupt(returnPc, RegisterTable.IRQ_VECTOR_L, RegisterTable.IRQ_VECTOR_H, false);
            getCPU().IRD = false;
        }

        public void handleInterrupt(int returnPc, int vectorLow, int vectorHigh, bool isBreak)
        {
            if(_isLog)
            ou($"handleInterrupt++>{returnPc:X8} ; vectorL->{vectorLow:X5}, vectorH->{vectorHigh:X5}, break: {isBreak}");
            // Set the break flag before pushing. 
            // or
            // IRQ & NMI clear break flag
            getCPU().BreakFlag = isBreak;


            // Push program counter + 1 onto the stack
            getStack().PushWord(returnPc); // PC high byte
            //getStack().PushWord(returnPc & 0xff);        // PC low byte
            getStack().PushByte(getState().getStatusFlag());
            // Set the Interrupt Disabled flag.  RTI will clear it.
            getCPU().IrqDisableFlag = true;

            // 65C02 & 65816 clear Decimal flag after pushing Processor status to the stack

            getCPU().DecimalModeFlag = false;
            
            // Push program counter + 1 onto the stack
            //getStack().PushWord(returnPc);
            //getStack().PushByte(getState().getStatusFlag());
            // Set the Interrupt Disabled flag. RTI will clear it.
            //getCPU().IrqDisableFlag = true;
            //getCPU().DecimalModeFlag = false;
            // Load interrupt vector address into PC
            //state.PC = isBreak ? state.BRK : readWord(vector);
            getState().PC = Memory.address(getBus().read(vectorLow, true), getBus().read(vectorHigh, true));

            if(getState().PC == ushort.MaxValue)
                throw new BiosException("Divide by zero Exception", "YOU JUST CREATED A BLACK HOLE!");
        }
        public void handleBrk(int returnPc)
        {
            if(_isLog)
            ou($"handleBrk++>{returnPc:X8}...");
            handleInterrupt(returnPc, RegisterTable.IRQ_VECTOR_L, RegisterTable.IRQ_VECTOR_H, true);
            getCPU().IRD = false;
        }
        private void ou(object s) => Log.nf(RCL.Wrap(s, Color.Red), RCL.Wrap("DEB", Color.Red));
    }
}
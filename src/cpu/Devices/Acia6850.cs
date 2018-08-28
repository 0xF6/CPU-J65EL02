namespace vm.devices
{
    using System;
    using cpu;
    using exceptions;

    public class Acia6850 : ACIA
    {
        public static readonly int ACIA_SIZE = 2;

        const int STAT_REG = 0;  // read-only
        const int CTRL_REG = 0;  // write-only

        const int RX_REG = 1;    // read-only
        const int TX_REG = 1;	// write-only


        public Acia6850(int address, CPU cp) : base(address, ACIA_SIZE, "ACIA6850", cp)
        {
        }

        public override void write(int address, int data)
        {
            switch (address)
            {
                case TX_REG:
                    txWrite(data);
                    break;
                case CTRL_REG:
                    setCommandRegister(data);
                    break;
                default:
                    throw new CorruptedMemoryException($"write: not found '0x{address:X4}' register", this);
            }
        }

        public override int read(int address, bool cpuAccess)
        {
            switch (address)
            {
                case RX_REG:
                    return rxRead(cpuAccess);
                case STAT_REG:
                    return statusReg(cpuAccess);

                default:
                    throw new CorruptedMemoryException($"read: not found '0x{address:X4}' register", this);
            }
        }

        private void setCommandRegister(int data)
        {
            // Bits 0 & 1 control the master reset
            if ((data & 0x01) != 0 && (data & 0x02) != 0)
                reset();

            // Bit 7 controls receiver IRQ behavior
            receiveIrqEnabled = (data & 0x80) != 0;
            // Bits 5 & 6 controls transmit IRQ behavior
            transmitIrqEnabled = (data & 0x20) != 0 && (data & 0x40) == 0;
        }

        public int statusReg(bool cpuAccess)
        {
            // TODO: Parity Error, Framing Error, DTR, and DSR flags.
            var stat = 0;
            if (rxFull && DateTime.Now.Ticks >= (lastRxRead + baudRateDelay))
                stat |= 0x01;
            if (txEmpty && DateTime.Now.Ticks >= (lastTxWrite + baudRateDelay))
                stat |= 0x02;
            if (overrun)
                stat |= 0x20;
            if (interrupt)
                stat |= 0x80;
            if (cpuAccess)
                interrupt = false;

            return stat;
        }
        private void reset()
        {
            lock (guarder)
            {
                overrun = false;
                rxFull = false;
                txEmpty = true;
                interrupt = false;
            }
        }
    }
}
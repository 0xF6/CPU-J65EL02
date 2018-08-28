namespace vm.devices
{
    using System;
    using cpu;
    using exceptions;

    public class Acia6551 : ACIA
    {
        public static readonly int ACIA_SIZE = 4;

        public const int DATA_REG = 0;
        public const int STAT_REG = 1;
        public const int CMND_REG = 2;
        public const int CTRL_REG = 3;


        /// <summary>
        /// Registers. These are ignored in the current implementation.
        /// </summary>
        private int commandRegister;
        private int controlRegister;
        public Acia6551(int address, CPU cpu) : base(address, ACIA_SIZE, "ACIA", cpu)
        {
        }

        public override void write(int address, int data)
        {
            switch (address)
            {
                case 0:
                    Log.nf($"txWrite $0x{address:X}, d: 0x{data:X}");
                    txWrite(data);
                    break;
                case 1:
                    Log.nf($"reset");
                    reset();
                    break;
                case 2:
                    Log.nf($"cmd_reg $0x{address:X}, d: 0x{data:X}");
                    setCommandRegister(data);
                    break;
                case 3:
                    Log.nf($"ctrl_reg $0x{address:X}, d: 0x{data:X}");
                    setControlRegister(data);
                    break;
                default:
                     throw new CorruptedMemoryException($"write: not found '0x{address:X4}' register", this);
            }
        }

        public override int read(int address, bool cpuAccess)
        {
            switch (address)
            {
                case DATA_REG:
                    return rxRead(cpuAccess);
                case STAT_REG:
                    return statusReg(cpuAccess);
                case CMND_REG:
                    return commandRegister;
                case CTRL_REG:
                    return controlRegister;
                default:
                    throw new CorruptedMemoryException($"read: not found '0x{address:X4}' register", this);
            }
        }

        private void setCommandRegister(int data)
        {
            commandRegister = data;

            // Bit 1 controls receiver IRQ behavior
            receiveIrqEnabled = (commandRegister & 0x02) == 0;
            // Bits 2 & 3 controls transmit IRQ behavior
            transmitIrqEnabled = (commandRegister & 0x08) == 0 && (commandRegister & 0x04) != 0;
        }

        private void setControlRegister(int data)
        {
            controlRegister = data;
            int rate = 0;

            // If the value of the data is 0, this is a request to reset,
            // otherwise it's a control update.

            if (data == 0)
            {
                reset();
            }
            else
            {
                // Mask the lower three bits to get the baud rate.
                int baudSelector = data & 0x0f;
                switch (baudSelector)
                {
                    case 0:
                        rate = 0;
                        break;
                    case 1:
                        rate = 50;
                        break;
                    case 2:
                        rate = 75;
                        break;
                    case 3:
                        rate = 110; // Real rate is actually 109.92
                        break;
                    case 4:
                        rate = 135; // Real rate is actually 134.58
                        break;
                    case 5:
                        rate = 150;
                        break;
                    case 6:
                        rate = 300;
                        break;
                    case 7:
                        rate = 600;
                        break;
                    case 8:
                        rate = 1200;
                        break;
                    case 9:
                        rate = 1800;
                        break;
                    case 10:
                        rate = 2400;
                        break;
                    case 11:
                        rate = 3600;
                        break;
                    case 12:
                        rate = 4800;
                        break;
                    case 13:
                        rate = 7200;
                        break;
                    case 14:
                        rate = 9600;
                        break;
                    case 15:
                        rate = 19200;
                        break;
                }

                setBaudRate(rate);
            }
        }

        public int statusReg(bool cpuAccess)
        {
            // TODO: Parity Error, Framing Error, DTR, and DSR flags.
            int stat = 0;
            if (rxFull && DateTime.Now.Ticks >= (lastRxRead + baudRateDelay))
            {
                stat |= 0x08;
            }
            if (txEmpty && DateTime.Now.Ticks >= (lastTxWrite + baudRateDelay))
            {
                stat |= 0x10;
            }
            if (overrun)
            {
                stat |= 0x04;
            }
            if (interrupt)
            {
                stat |= 0x80;
            }

            if (cpuAccess)
            {
                interrupt = false;
            }

            return stat;
        }


        private void reset()
        {
            lock (guarder)
            {
                txChar = 0;
                txEmpty = true;
                rxChar = 0;
                rxFull = false;
                receiveIrqEnabled = false;
                transmitIrqEnabled = false;
                interrupt = false;
            }
        }


    }
}
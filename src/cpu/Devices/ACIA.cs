namespace vm.devices
{
    using System;
    using cpu;

    public abstract class ACIA : Device
    {
        protected readonly object guarder = new object();
        private string name { get; set; }
        public override string getName() => name;


        protected int baseAddress;

        protected bool receiveIrqEnabled = false;
        protected bool transmitIrqEnabled = false;
        protected bool overrun = false;
        protected bool interrupt = false;

        protected long lastTxWrite = 0;
        protected long lastRxRead = 0;
        protected int baudRate { get; set; } = 0;
        protected long baudRateDelay = 0;

        /**
         * Read/Write buffers
         */
        protected int rxChar = 0;
        protected int txChar = 0;

        protected bool rxFull = false;
        protected bool txEmpty = true;


        protected ACIA(int address, int size, string name, CPU cp) : base(address, address + size - 1, cp)
        {
            this.name = name;
            this.baseAddress = address;
        }

        public void setBaudRate(int rate)
        {
            this.baudRate = rate;
            this.baudRateDelay = calculateBaudRateDelay();
        }
        protected long calculateBaudRateDelay()
        {
            if (baudRate > 0)
            {
                // TODO: This is a pretty rough approximation based on 8 bits per character,
                // and 1/baudRate per bit. It could certainly be improved
                return (long)((1.0 / baudRate) * 1000000000 * 8);
            }
            return 0;
        }

        public int rxRead(bool cpuAccess)
        {
            lock (guarder)
            {
                if (cpuAccess)
                {
                    lastRxRead = DateTime.Now.Ticks;
                    overrun = false;
                    rxFull = false;
                }
                return rxChar;
            }
        }
        public void rxWrite(int data)
        {
            lock (guarder)
            {
                if (rxFull)
                    overrun = true;

                rxFull = true;

                if (receiveIrqEnabled)
                {
                    interrupt = true;
                    //cpu.state.assertIrq();
                }

                rxChar = data;
            }
        }

        public int txRead(bool cpuAccess)
        {
            lock (guarder)
            {
                if (cpuAccess)
                {
                    txEmpty = true;

                    if (transmitIrqEnabled)
                    {
                        interrupt = true;
                        //cpu.Bus.assertIrq();
                    }
                }
                return txChar;
            }
        }

        public void txWrite(int data)
        {
            lock (guarder)
            {
                lastTxWrite = DateTime.Now.Ticks;
                txChar = data;
                txEmpty = false;
            }
        }

        public bool hasTxChar() => !txEmpty;
        public bool hasRxChar() => rxFull;
    }
}
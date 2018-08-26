namespace vm
{
    using System.IO;
    using System.Threading;
    using components;
    using cpu;
    using devices;
    using exceptions;

    public class Machine
    {
        private bool isRunning = false;
        private readonly Semaphore interruptWait = new Semaphore(2, 2);

        private readonly Bus bus;
        private readonly CPU cpu;
        private readonly RedBus redBus;

        private int defaultDriveId = 2;
        private int defaultMonitorId = 1;

        /// <summary>
        /// Constructs the machine with and empty 8k of memory.
        /// </summary>
        public Machine() : this(null, 0x2000) { }
        /// <summary>
        /// Constructs the machine with and empty memory of the given size.
        /// </summary>
        public Machine(int coreRamSize) : this(null, coreRamSize) { }
        /// <summary>
        /// Constructs the machine with the given RAM size and load a bootloader into memory.
        /// The bootloader is loaded at address 0x400 to 0x500.
        /// </summary>
        /// <param name="bootloader">
        /// The path to the bootloader file
        /// </param>
        /// <param name="coreRamSize">
        /// The size of RAM in bytes
        /// </param>
        public Machine(string bootloader, int coreRamSize)
        {
            try
            {
                Log.nf("bios loaded");
                cpu = new CPU();
                redBus = new RedBus(cpu);
                bus = new Bus(redBus);
                cpu.Bus = (bus);
                var deb = new Debugger(cpu);

                cpu.linkDebugger(deb);

                var ram = new Memory(0x0000, coreRamSize - 1, cpu);
                if (bootloader != null)
                    ram.loadFromFile(bootloader, 0x400, 0x100, "bootloader");  
                bus.AddDevice(ram);
                
            }
            catch (IOException e)
            {
                throw new RuntimeException("runtime exception in current machine.", e);
            }
            reset();
            warmUpCPU();
        }

        public void warmUpCPU()
        {
            cpu.state.POR   = 0x2000;
            cpu.state.BRK   = 0x2000;
            cpu.state.SP    = 0x200;
            cpu.state.PC    = 0x400;
            cpu.state.R     = 0x300;
        }
        public void run()
        {
            this.isRunning = true;
            do
            {
                step();
            } while (this.isRunning);
        }
        public void reset()
        {
            stop();
            cpu.RESET();
            bus.write(0, defaultDriveId);
            bus.write(1, defaultMonitorId);
        }
        public void stop()
        {
            isRunning = false;
        }

        public void step()
        {
            this.cpu.cycle();
            this.bus.update();
            if (this.cpu.state.signalStop)
            {
                this.stop();
                return;
            }
            if (this.cpu.state.intWait)
            {
                this.cpu.state.irqAsserted = true;
            }
        }
    }
}
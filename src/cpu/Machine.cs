namespace vm
{
    using System;
    using System.IO;
    using components;
    using cpu;
    using devices;
    using exceptions;

    public class Machine
    {
        private bool isRunning = false;

        private readonly Bus bus;
        private readonly CPU cpu;
        private readonly RedBus redBus;

        private int defaultDriveId = 2;
        private int defaultMonitorId = 1;

        /// <summary>
        /// Constructs the machine with and empty 8k of memory.
        /// </summary>
        public Machine() : this(null, null, 0x2000) { }
        /// <summary>
        /// Constructs the machine with and empty memory of the given size.
        /// </summary>
        public Machine(int coreRamSize) : this(null, null, coreRamSize) { }
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
        public Machine(string bootloader, string os, int coreRamSize)
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
                    ram.loadFromFile(bootloader, 0x0300, 0x52, "bootloader");
                if (os != null)
                    ram.loadFromFile(os, 0x0300, 0x52, "os");
                bus.AddDevice(ram);
                //bus.AddDevice(new Acia6850(0x8800, cpu));
                bus.AddDevice(new Acia6551(0x8800, cpu));
                bus.AddDevice(new CRTC(0x9000, cpu, ram));
                //bus.AddDevice(new WIFICard(0x10000, cpu));
                bus.AddDevice(new WirelessTerminal(0x9500, cpu));
                
            }
            catch (IOException e)
            {
                throw new RuntimeException("runtime exception in current machine.", e);
            }
            reset();
            coldUpCPU();
            try
            {
                //coldUpDevices();
            }
            catch (Exception e)
            {
                cpu.Hault(e);
            }
            //warmUpCPU();
        }

        public void coldUpCPU()
        {
            cpu.state.POR = 0x2000;
            cpu.state.BRK = 0x2000;
            cpu.state.SP = 0x200;
            cpu.state.PC = 0x0300;
            cpu.state.R = 0x300;

            //cpu.state.emulationFlag = true;
            //cpu.state.decimalModeFlag = true;
        }

        public void coldUpDevices()
        {
            // 0x9500 - start address of wireless display
            if(bus.read(0x9503, true) == 0)
                throw new CorruptedMemoryException("invalid state", bus.findDevice(0x11000));
            if(bus.read(0x9502, true) == 0x0)
                bus.write(0x9500, 0x0);
            if(bus.read(0x9502, true) != 0x1)
                throw new CorruptedMemoryException("invalid state", bus.findDevice(0x11000));
            bus.write(0x9501, 0x0);
        }
        public void run()
        {
            this.isRunning = true;
            do
            {
                try
                {
                    step();
                }
                catch (Exception e)
                {
                    cpu.Hault(e);
                    this.isRunning = false;
                    break;
                }
            } while (this.isRunning);
        }
        public void reset()
        {
            stop();
            //cpu.RESET();
            //bus.write(0, defaultDriveId);
            //bus.write(1, defaultMonitorId);
        }
        public void stop()
        {
            isRunning = false;
        }

        public void step()
        {
            this.cpu.cycle();

            this.bus.update();
            var virtualTerm = bus.findDevice(0x8800);
            var wirelessTerm = bus.findDevice(0x11000);
            // Read from the ACIA and immediately update the console if there's
            // output ready.
            if (virtualTerm is ACIA acia && acia.hasTxChar() && wirelessTerm is WirelessTerminal term)
                term.write(0x2, acia.txRead(true));
            if (this.cpu.state.signalStop)
            {
                this.stop();
                return;
            }
            if (this.cpu.state.intWait)
                this.cpu.state.irqAsserted = true;
        }
    }
}
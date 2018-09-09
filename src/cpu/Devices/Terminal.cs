namespace vm.devices
{
    using cpu;
    using exceptions;
    using Flurl;
    using Flurl.Http;

    public class WirelessTerminal : RemoteDevice
    {
        public const int REGINIT = 0x0;
        public const int REGCON = 0x1;
        public const int REGWR = 0x2;

        public WirelessTerminal(int startAddress, CPU cp) : base(startAddress, startAddress + 6, cp)  { }

        public override void write(int address, int data)
        {
            switch (address)
            {
                case REGINIT:
                    init();
                    break;
                case REGCON:
                    conn();
                    break;
                case REGWR:
                    txRw((char) data);
                    break;
            }
        }

        private void init()
        {
            conn_str = $"{getPhysicalAddress()}:{getPhysicalPort()}";
            status = 0x1;
        }
        private void conn()
        {
            try
            {
                conn_str.AppendPathSegment("status").GetJsonAsync().Wait();
                status = 0x2;
            }
            catch
            {
                throw new CorruptedMemoryException("Invalid driver data.", this);
            }

        }
        private void txRw(char t)
        {
            try
            {
                conn_str.AppendPathSegment("char").PostJsonAsync(new {@char = t}).ReceiveJson().Wait();
                status = 0x3;
            }
            catch
            {
                throw new MemoryViolationException("err access; memory is not writable", this);
            }
        }

        public override int read(int address, bool cpuAccess)
        {
            if (!cpuAccess)
                throw new MemoryViolationException("err access; only cpu has read access;", this);
            switch (address)
            {
                case 0x2: return status;
                case 0x3: return getPhysicalPort();
            }
            return -0x1;
        }

        public override string getPhysicalAddress() => "http://localhost";

        public override int getPhysicalPort() => 8666;


        private string conn_str;
        private int status;

        public override string getName() => "Wireless Display";

    }
}
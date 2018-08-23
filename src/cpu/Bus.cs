namespace cpu
{
    using System;
    using System.Collections.Generic;
    using Devices;

    public class Bus
    {
        public RedBus RedBus { get; private set; }
        private List<Device> devices { get; set; }

        private int[] boundaries { get; set; }

        public Bus(RedBus redBus)
        {
            this.RedBus = redBus;
            this.devices = new List<Device>();
            this.boundaries = new int[0];
        }

        public void AddDevice(Device device)
        {
            this.devices.Add(device);
            var newBoundaries = new int[this.boundaries.Length + 1];
            Array.Copy(this.boundaries, 0, newBoundaries, 1, this.boundaries.Length);
            newBoundaries[0] = device.StartAddress;
            Array.Sort(newBoundaries);
            this.devices.Sort();
            this.boundaries = newBoundaries;
        }

        public void write(int address, int data)
        {
            var device = findDevice(address);
            device.write(address - device.StartAddress, data);
        }

        public int read(int address, bool cpuAccess)
        {
            var device = findDevice(address);
            return device.read(address - device.StartAddress, cpuAccess) & 0xff;
        }

        public void update()
        {
            this.RedBus.updatePeripheral();
        }
        private Device findDevice(int address)
        {
            if (this.RedBus.inRange(address))
                return this.RedBus;
            var idx = Array.BinarySearch(this.boundaries, address);
            if (idx < 0) idx = -idx - 2;
            return this.devices[idx];
        }
    }
}
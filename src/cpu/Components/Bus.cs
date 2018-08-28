namespace vm.components
{
    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using devices;
    using exceptions;
    using RC.Framework.Screens;

    public class Bus
    {
        public RedBus RedBus { get; private set; }
        public List<Device> devices { get; private set; }
        public int BusCapacity { get; private set; }

        private int[] boundaries { get; set; }

        public Bus(RedBus redBus) : this(redBus, 0x10) { }

        public Bus(RedBus redBus, int size)
        {
            RedBus = redBus;
            devices = new List<Device>(size);
            boundaries = new int[0];
            BusCapacity = size;
        }

        public void AddDevice(Device device)
        {
            if (devices.Count == BusCapacity)
                throw new OverflowBusCapacityException();
            Log.nf($"dev->{device.GetType().Name}->init", RCL.Wrap("BUS", Color.Aquamarine));
            devices.Add(device);
            var newBoundaries = new int[boundaries.Length + 1];
            Array.Copy(boundaries, 0, newBoundaries, 1, boundaries.Length);
            newBoundaries[0] = device.StartAddress;
            Array.Sort(newBoundaries);
            devices.Sort();
            boundaries = newBoundaries;
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

        public void update() => RedBus.updatePeripheral();

        public Device findDevice(int address)
        {
            if (RedBus.inRange(address))
                return RedBus;
            var idx = Array.BinarySearch(boundaries, address);
            if (idx < 0) idx = -idx - 2;
            if (idx < 0) return new CorruptedDevice();
            return devices[idx];
        }

        public override string ToString()
        {
            return $"@[{-1:X}-{-1:X}] - c:{devices.Count}/{BusCapacity}";
        }
    }
}
namespace vm.exceptions
{
    using System;
    using System.Runtime.Serialization;
    using devices;

    public class CorruptedMemoryException : Exception
    {
        public override string Message => $"{Msg} in {_cautchDevice.getName()} device at @0x{_cautchDevice.StartAddress:X5}-0x{_cautchDevice.endAddress:X5}";
        private readonly string Msg;
        private readonly Device _cautchDevice;

        public CorruptedMemoryException(string message, Device cautchDevice) : base(message)
        {
            Msg = message;
            _cautchDevice = cautchDevice;
        }
    }
}
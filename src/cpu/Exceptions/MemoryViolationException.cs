namespace vm.exceptions
{
    using devices;

    public class MemoryViolationException : CorruptedMemoryException
    {
        public MemoryViolationException(string message, Device cautchDevice) : base(message, cautchDevice)
        {
        }
    }
}
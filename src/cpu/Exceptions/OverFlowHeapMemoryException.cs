namespace vm.exceptions
{
    using System;
    public class OverFlowHeapMemoryException : Exception
    {
        public OverFlowHeapMemoryException(string message) : base(message)
        {
        }
    }
}
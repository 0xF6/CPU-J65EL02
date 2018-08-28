namespace vm.exceptions
{
    using System;
    using System.Runtime.Serialization;

    public class OverflowBusCapacityException : Exception
    {
        public OverflowBusCapacityException()
        {
        }

        public OverflowBusCapacityException(string message) : base(message)
        {
        }

        public OverflowBusCapacityException(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected OverflowBusCapacityException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}
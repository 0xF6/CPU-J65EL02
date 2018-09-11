namespace vm.exceptions
{
    using System;
    public class BiosException : Exception
    {
        public string Description { get; set; }
        public BiosException(string message, string desc) : base(message)
        {
            Description = desc;
        }
    }
}
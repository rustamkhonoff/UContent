using System;

namespace UContent
{
    public sealed class ContentOperationException : Exception
    {
        public string Operation { get; }
        public object Key { get; }

        public ContentOperationException(string operation, object key, Exception innerException = null)
            : base($"Content operation '{operation}' failed for key '{key}'.", innerException)
        {
            Operation = operation;
            Key = key;
        }
    }
}
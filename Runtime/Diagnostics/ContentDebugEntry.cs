using System;

namespace UContent.Diagnostics
{
    public sealed class ContentDebugEntry
    {
        public int Id { get; }
        public ContentDebugType Type { get; }
        public string Name { get; }
        public string Key { get; }
        public DateTime CreatedAtUtc { get; }
        public string StackTrace { get; }

        internal ContentDebugEntry(int id, ContentDebugType type, string name, string key, string stackTrace)
        {
            Id = id;
            Type = type;
            Name = name;
            Key = key;
            StackTrace = stackTrace;
            CreatedAtUtc = DateTime.UtcNow;
        }
    }
}
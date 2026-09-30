using System;

namespace UContent.Internal
{
    internal readonly struct ContentRequestKey : IEquatable<ContentRequestKey>
    {
        public Type Type { get; }
        public object Key { get; }

        public ContentRequestKey(Type type, object key)
        {
            Type = type;
            Key = key;
        }

        public bool Equals(ContentRequestKey other)
        {
            return Type == other.Type && Equals(Key, other.Key);
        }

        public override bool Equals(object obj)
        {
            return obj is ContentRequestKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((Type != null ? Type.GetHashCode() : 0) * 397) ^ (Key != null ? Key.GetHashCode() : 0);
            }
        }
    }
}
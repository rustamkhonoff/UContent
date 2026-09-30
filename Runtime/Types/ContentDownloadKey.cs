using System;
using System.Collections.Generic;

namespace UContent.Internal
{
    internal readonly struct ContentDownloadKey : IEquatable<ContentDownloadKey>
    {
        private readonly object[] _keys;

        public ContentMergeMode MergeMode { get; }

        private ContentDownloadKey(object[] keys, ContentMergeMode mergeMode)
        {
            _keys = keys;
            MergeMode = mergeMode;
        }

        public static ContentDownloadKey Single(object key)
        {
            return new ContentDownloadKey(new[] { key }, ContentMergeMode.UseFirst);
        }

        public static ContentDownloadKey Multiple(IReadOnlyList<object> keys, ContentMergeMode mergeMode)
        {
            var copy = new object[keys.Count];

            for (var i = 0; i < keys.Count; i++)
                copy[i] = keys[i];

            return new ContentDownloadKey(copy, mergeMode);
        }

        public bool Equals(ContentDownloadKey other)
        {
            if (MergeMode != other.MergeMode)
                return false;

            if (_keys == null || other._keys == null)
                return _keys == other._keys;

            if (_keys.Length != other._keys.Length)
                return false;

            for (var i = 0; i < _keys.Length; i++)
            {
                if (!Equals(_keys[i], other._keys[i]))
                    return false;
            }

            return true;
        }

        public override bool Equals(object obj)
        {
            return obj is ContentDownloadKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = (int)MergeMode;

                if (_keys == null)
                    return hash;

                for (var i = 0; i < _keys.Length; i++)
                    hash = (hash * 397) ^ (_keys[i] != null ? _keys[i].GetHashCode() : 0);

                return hash;
            }
        }
    }
}
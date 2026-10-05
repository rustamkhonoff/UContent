using System;
using System.Threading;
using UContent.Diagnostics;

namespace UContent
{
    public sealed class ContentHandle<T> : IDisposable
    {
        private readonly T _value;
        private readonly int _debugId;

        private Action _release;

        public object Key { get; }
        public bool IsReleased => Volatile.Read(ref _release) == null;

        public T Value
        {
            get
            {
                if (IsReleased)
                    throw new ObjectDisposedException(nameof(ContentHandle<T>));

                return _value;
            }
        }

        internal ContentHandle(object key, T value, Action release)
        {
            Key = key;
            _value = value;
            _release = release ?? throw new ArgumentNullException(nameof(release));
            _debugId = ContentDiagnostics.Register(ContentDebugType.Handle, key, typeof(T).Name);
        }

        public bool TryGetValue(out T value)
        {
            if (IsReleased)
            {
                value = default;
                return false;
            }

            value = _value;
            return true;
        }

        public void Dispose()
        {
            var release = Interlocked.Exchange(ref _release, null);
            if (release == null)
                return;

            try
            {
                release();
            }
            finally
            {
                ContentDiagnostics.Release(_debugId);
            }
        }
    }
}

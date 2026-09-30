using System;
using System.Threading;

namespace UContent
{
    public sealed class ContentHandle<T> : IDisposable
    {
        private readonly T m_value;
        private Action m_release;

        public object Key { get; }
        public bool IsReleased => Volatile.Read(ref m_release) == null;

        public T Value
        {
            get
            {
                if (IsReleased)
                    throw new ObjectDisposedException(nameof(ContentHandle<T>));

                return m_value;
            }
        }

        internal ContentHandle(object key, T value, Action release)
        {
            Key = key;
            m_value = value;
            m_release = release ?? throw new ArgumentNullException(nameof(release));
        }

        public bool TryGetValue(out T value)
        {
            if (IsReleased)
            {
                value = default;
                return false;
            }

            value = m_value;
            return true;
        }

        public void Dispose()
        {
            Interlocked.Exchange(ref m_release, null)?.Invoke();
        }
    }
}
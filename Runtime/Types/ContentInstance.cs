using System;
using System.Threading;
using UnityEngine;

namespace UContent
{
    public sealed class ContentInstance : IDisposable
    {
        private readonly GameObject m_instance;
        private Action m_release;

        public object Key { get; }
        public bool IsReleased => Volatile.Read(ref m_release) == null;

        public GameObject Instance
        {
            get
            {
                if (IsReleased)
                    throw new ObjectDisposedException(nameof(ContentInstance));

                return m_instance;
            }
        }

        public Transform Transform => Instance.transform;

        internal ContentInstance(object key, GameObject instance, Action release)
        {
            Key = key;
            m_instance = instance;
            m_release = release ?? throw new ArgumentNullException(nameof(release));
        }

        public void Dispose()
        {
            Interlocked.Exchange(ref m_release, null)?.Invoke();
        }
    }
}
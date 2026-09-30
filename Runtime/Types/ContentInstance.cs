using System;
using System.Threading;
using UnityEngine;

namespace UContent
{
    public sealed class ContentInstance : IDisposable
    {
        private readonly GameObject _instance;

        private Action _release;

        public object Key { get; }
        public bool IsReleased => Volatile.Read(ref _release) == null;

        public GameObject Instance
        {
            get
            {
                if (IsReleased)
                    throw new ObjectDisposedException(nameof(ContentInstance));

                return _instance;
            }
        }

        public Transform Transform => Instance.transform;

        internal ContentInstance(object key, GameObject instance, Action release)
        {
            Key = key;
            _instance = instance;
            _release = release ?? throw new ArgumentNullException(nameof(release));
        }

        public void Dispose()
        {
            Interlocked.Exchange(ref _release, null)?.Invoke();
        }
    }
}
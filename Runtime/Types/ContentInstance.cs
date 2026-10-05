using System;
using System.Threading;
using UContent.Diagnostics;
using UnityEngine;

namespace UContent
{
    public sealed class ContentInstance : IDisposable
    {
        private readonly GameObject _instance;
        private readonly int _debugId;

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
            _debugId = ContentDiagnostics.Register(ContentDebugType.Instance, key);
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

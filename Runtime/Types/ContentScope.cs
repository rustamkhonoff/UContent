using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace UContent
{
    public sealed class ContentScope : IDisposable
    {
        private readonly IContentService _content;
        private readonly List<IDisposable> _handles = new();

        private bool _disposed;

        public string Name { get; }
        public int Count => _handles.Count;
        public bool IsDisposed => _disposed;

        internal ContentScope(IContentService content, string name)
        {
            _content = content;
            Name = string.IsNullOrEmpty(name) ? "ContentScope" : name;
        }

        public async UniTask<T> LoadAsync<T>(object key, CancellationToken cancellationToken = default) where T : UnityEngine.Object
        {
            ThrowIfDisposed();

            var handle = await _content.LoadAsync<T>(key, cancellationToken);
            Register(handle);

            return handle.Value;
        }

        public async UniTask<IReadOnlyList<T>> LoadAllAsync<T>(object key, CancellationToken cancellationToken = default) where T : UnityEngine.Object
        {
            ThrowIfDisposed();

            var handle = await _content.LoadAllAsync<T>(key, cancellationToken);
            Register(handle);

            return handle.Value;
        }

        public async UniTask<IReadOnlyList<T>> LoadAllAsync<T>(IEnumerable<object> keys, ContentMergeMode mergeMode = ContentMergeMode.Union, CancellationToken cancellationToken = default) where T : UnityEngine.Object
        {
            ThrowIfDisposed();

            var handle = await _content.LoadAllAsync<T>(keys, mergeMode, cancellationToken);
            Register(handle);

            return handle.Value;
        }

        public async UniTask<GameObject> InstantiateAsync(object key, Transform parent = null, bool instantiateInWorldSpace = false, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            var instance = await _content.InstantiateAsync(key, parent, instantiateInWorldSpace, cancellationToken);
            Register(instance);

            return instance.Instance;
        }

        public async UniTask<GameObject> InstantiateAsync(object key, Vector3 position, Quaternion rotation, Transform parent = null, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            var instance = await _content.InstantiateAsync(key, position, rotation, parent, cancellationToken);
            Register(instance);

            return instance.Instance;
        }

        public void Clear()
        {
            for (var i = _handles.Count - 1; i >= 0; i--)
            {
                try
                {
                    _handles[i].Dispose();
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }

            _handles.Clear();
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            Clear();
        }

        private void Register(IDisposable handle)
        {
            if (_disposed)
            {
                handle.Dispose();
                throw new ObjectDisposedException(Name);
            }

            _handles.Add(handle);
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(Name);
        }
    }
}
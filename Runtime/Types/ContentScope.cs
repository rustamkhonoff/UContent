using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace UContent
{
    public sealed class ContentScope : IDisposable
    {
        private readonly IContentService m_content;
        private readonly List<IDisposable> m_contentHandles = new();

        private bool m_disposed;

        public string Name { get; }
        public int Count => m_contentHandles.Count;
        public bool IsDisposed => m_disposed;

        internal ContentScope(IContentService content, string name)
        {
            m_content = content;
            Name = string.IsNullOrEmpty(name) ? "ContentScope" : name;
        }

        public async UniTask<T> LoadAsync<T>(object key, CancellationToken cancellationToken = default) where T : UnityEngine.Object
        {
            ThrowIfDisposed();

            var handle = await m_content.LoadAsync<T>(key, cancellationToken);
            Register(handle);

            return handle.Value;
        }

        public async UniTask<IReadOnlyList<T>> LoadAllAsync<T>(object key, CancellationToken cancellationToken = default) where T : UnityEngine.Object
        {
            ThrowIfDisposed();

            var handle = await m_content.LoadAllAsync<T>(key, cancellationToken);
            Register(handle);

            return handle.Value;
        }

        public async UniTask<GameObject> InstantiateAsync(object key, Transform parent = null, bool instantiateInWorldSpace = false, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            var instance = await m_content.InstantiateAsync(key, parent, instantiateInWorldSpace, cancellationToken);
            Register(instance);

            return instance.Instance;
        }

        public async UniTask<GameObject> InstantiateAsync(object key, Vector3 position, Quaternion rotation, Transform parent = null, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            var instance = await m_content.InstantiateAsync(key, position, rotation, parent, cancellationToken);
            Register(instance);

            return instance.Instance;
        }

        public void Clear()
        {
            for (var i = m_contentHandles.Count - 1; i >= 0; i--)
            {
                try
                {
                    m_contentHandles[i].Dispose();
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }

            m_contentHandles.Clear();
        }

        public void Dispose()
        {
            if (m_disposed)
                return;

            m_disposed = true;
            Clear();
        }

        private void Register(IDisposable content)
        {
            if (m_disposed)
            {
                content.Dispose();
                throw new ObjectDisposedException(Name);
            }

            m_contentHandles.Add(content);
        }

        private void ThrowIfDisposed()
        {
            if (m_disposed)
                throw new ObjectDisposedException(Name);
        }
    }
}
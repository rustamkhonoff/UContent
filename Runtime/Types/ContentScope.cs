using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UContent.Diagnostics;
using UnityEngine;

namespace UContent
{
    public sealed class ContentScope : IDisposable
    {
        private readonly IContentService m_content;
        private readonly List<IDisposable> m_handles = new();
        private readonly int m_debugId;

        private bool m_disposed;

        public string Name { get; }
        public int Count => m_handles.Count;
        public bool IsDisposed => m_disposed;

        internal ContentScope(IContentService content, string name)
        {
            m_content = content;
            Name = string.IsNullOrEmpty(name) ? "ContentScope" : name;
            m_debugId = ContentDiagnostics.Register(ContentDebugType.Scope, null, Name);
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

        public async UniTask<IReadOnlyList<T>> LoadAllAsync<T>(IEnumerable<object> keys, ContentMergeMode mergeMode = ContentMergeMode.Union, CancellationToken cancellationToken = default) where T : UnityEngine.Object
        {
            ThrowIfDisposed();

            var handle = await m_content.LoadAllAsync<T>(keys, mergeMode, cancellationToken);
            Register(handle);

            return handle.Value;
        }

        public async UniTask<GameObject> InstantiateAsync(object key, Transform parent = null, bool instantiateInWorldSpace = false, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            ContentInstance instance = await m_content.InstantiateAsync(key, parent, instantiateInWorldSpace, cancellationToken);
            Register(instance);

            return instance.Instance;
        }

        public async UniTask<GameObject> InstantiateAsync(object key, Vector3 position, Quaternion rotation, Transform parent = null, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            ContentInstance instance = await m_content.InstantiateAsync(key, position, rotation, parent, cancellationToken);
            Register(instance);

            return instance.Instance;
        }

        public void Clear()
        {
            for (int i = m_handles.Count - 1; i >= 0; i--)
            {
                try
                {
                    m_handles[i].Dispose();
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }

            m_handles.Clear();
        }

        public void Dispose()
        {
            if (m_disposed)
                return;

            m_disposed = true;

            Clear();
            ContentDiagnostics.Release(m_debugId);
        }

        private void Register(IDisposable handle)
        {
            if (m_disposed)
            {
                handle.Dispose();
                throw new ObjectDisposedException(Name);
            }

            m_handles.Add(handle);
        }

        private void ThrowIfDisposed()
        {
            if (m_disposed)
                throw new ObjectDisposedException(Name);
        }
    }
}
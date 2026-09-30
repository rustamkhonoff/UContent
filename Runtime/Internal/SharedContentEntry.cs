using System;
using Cysharp.Threading.Tasks;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace UContent
{
    internal interface ISharedContentEntry { }

    internal sealed class SharedContentEntry<T> : ISharedContentEntry where T : UnityEngine.Object
    {
        private readonly object m_key;
        private readonly Action m_onReleased;
        private readonly UniTaskCompletionSource<T> m_completion = new();

        private AsyncOperationHandle<T> m_handle;
        private int m_references;

        private bool m_started;
        private bool m_completed;
        private bool m_released;

        public UniTask<T> Task => m_completion.Task;
        public int References => m_references;

        public SharedContentEntry(object key, Action onReleased)
        {
            m_key = key;
            m_onReleased = onReleased;
        }

        public void Retain()
        {
            if (m_released)
                throw new ObjectDisposedException(nameof(SharedContentEntry<T>));

            m_references++;
        }

        public void Start()
        {
            if (m_started)
                return;

            m_started = true;
            RunAsync().Forget();
        }

        public void Release()
        {
            if (m_released)
                return;

            if (m_references > 0)
                m_references--;

            TryRelease();
        }

        private async UniTask RunAsync()
        {
            m_handle = Addressables.LoadAssetAsync<T>(m_key);

            try
            {
                var asset = await m_handle.ToUniTask();

                if (m_handle.Status != AsyncOperationStatus.Succeeded || asset == null)
                {
                    Fail(new ContentOperationException("Load", m_key, m_handle.OperationException));
                    return;
                }

                m_completed = true;
                m_completion.TrySetResult(asset);

                TryRelease();
            }
            catch (Exception exception)
            {
                Fail(exception);
            }
        }

        private void Fail(Exception exception)
        {
            m_completed = true;
            m_completion.TrySetException(exception);

            ReleaseHandle();
        }

        private void TryRelease()
        {
            if (!m_completed || m_references > 0)
                return;

            ReleaseHandle();
        }

        private void ReleaseHandle()
        {
            if (m_released)
                return;

            m_released = true;

            if (m_handle.IsValid())
                Addressables.Release(m_handle);

            m_onReleased?.Invoke();
        }
    }
}
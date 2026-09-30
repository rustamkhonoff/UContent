using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace UContent.Internal
{
    internal sealed class SharedDownloadOperation
    {
        private readonly object m_key;
        private readonly Action m_onCompleted;
        private readonly List<IProgress<ContentDownloadProgress>> m_progressListeners = new();
        private readonly UniTaskCompletionSource<bool> m_completion = new();

        private AsyncOperationHandle m_handle;

        public UniTask Task => m_completion.Task.AsUniTask();

        public SharedDownloadOperation(object key, Action onCompleted)
        {
            m_key = key;
            m_onCompleted = onCompleted;
        }

        public void Start()
        {
            RunAsync().Forget();
        }

        public void AddProgress(IProgress<ContentDownloadProgress> progress)
        {
            if (progress == null || m_progressListeners.Contains(progress))
                return;

            m_progressListeners.Add(progress);
        }

        public void RemoveProgress(IProgress<ContentDownloadProgress> progress)
        {
            if (progress != null)
                m_progressListeners.Remove(progress);
        }

        private async UniTask RunAsync()
        {
            m_handle = Addressables.DownloadDependenciesAsync(m_key, false);

            try
            {
                while (!m_handle.IsDone)
                {
                    ReportProgress(false);
                    await UniTask.Yield();
                }

                if (m_handle.Status != AsyncOperationStatus.Succeeded)
                    throw new ContentOperationException("Download", m_key, m_handle.OperationException);

                ReportProgress(true);
                m_completion.TrySetResult(true);
            }
            catch (Exception exception)
            {
                m_completion.TrySetException(exception);
            }
            finally
            {
                if (m_handle.IsValid())
                    Addressables.Release(m_handle);

                m_onCompleted?.Invoke();
            }
        }

        private void ReportProgress(bool isDone)
        {
            var status = m_handle.GetDownloadStatus();
            var progress = new ContentDownloadProgress(status.DownloadedBytes, status.TotalBytes, isDone);

            for (var i = 0; i < m_progressListeners.Count; i++)
                m_progressListeners[i]?.Report(progress);
        }
    }
}
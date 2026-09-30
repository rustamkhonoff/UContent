using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace UContent.Internal
{
    internal sealed class SharedDownloadOperation
    {
        private readonly object _key;
        private readonly Func<AsyncOperationHandle> _start;
        private readonly Action _onCompleted;
        private readonly List<IProgress<ContentDownloadProgress>> _progressListeners = new();
        private readonly UniTaskCompletionSource<bool> _completion = new();

        private AsyncOperationHandle _handle;
        private bool _started;

        public UniTask<bool> Task => _completion.Task;

        public SharedDownloadOperation(object key, Func<AsyncOperationHandle> start, Action onCompleted)
        {
            _key = key;
            _start = start;
            _onCompleted = onCompleted;
        }

        public void Start()
        {
            if (_started)
                return;

            _started = true;
            RunAsync().Forget();
        }

        public void AddProgress(IProgress<ContentDownloadProgress> progress)
        {
            if (progress == null || _progressListeners.Contains(progress))
                return;

            _progressListeners.Add(progress);
        }

        public void RemoveProgress(IProgress<ContentDownloadProgress> progress)
        {
            if (progress != null)
                _progressListeners.Remove(progress);
        }

        private async UniTaskVoid RunAsync()
        {
            _handle = _start();

            try
            {
                long lastDownloadedBytes = -1;
                long lastTotalBytes = -1;

                while (!_handle.IsDone)
                {
                    var status = _handle.GetDownloadStatus();

                    if (status.DownloadedBytes != lastDownloadedBytes || status.TotalBytes != lastTotalBytes)
                    {
                        ReportProgress(status.DownloadedBytes, status.TotalBytes, false);
                        lastDownloadedBytes = status.DownloadedBytes;
                        lastTotalBytes = status.TotalBytes;
                    }

                    await UniTask.Yield();
                }

                if (_handle.Status != AsyncOperationStatus.Succeeded)
                    throw new ContentOperationException("Download", _key, _handle.OperationException);

                var finalStatus = _handle.GetDownloadStatus();
                ReportProgress(finalStatus.DownloadedBytes, finalStatus.TotalBytes, true);

                _completion.TrySetResult(true);
            }
            catch (Exception exception)
            {
                if (exception is ContentOperationException)
                    _completion.TrySetException(exception);
                else
                    _completion.TrySetException(new ContentOperationException("Download", _key, exception));
            }
            finally
            {
                if (_handle.IsValid())
                    UnityEngine.AddressableAssets.Addressables.Release(_handle);

                _onCompleted?.Invoke();
            }
        }

        private void ReportProgress(long downloadedBytes, long totalBytes, bool isDone)
        {
            var progress = new ContentDownloadProgress(downloadedBytes, totalBytes, isDone);

            for (var i = 0; i < _progressListeners.Count; i++)
                _progressListeners[i]?.Report(progress);
        }
    }
}
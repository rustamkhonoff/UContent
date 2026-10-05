using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
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
        private ContentDownloadProgress? _lastProgress;

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
            if (progress == null)
                return;

            _progressListeners.Add(progress);
            if (_lastProgress.HasValue)
                ReportTo(progress, _lastProgress.Value);
        }

        public void RemoveProgress(IProgress<ContentDownloadProgress> progress)
        {
            if (progress != null)
                _progressListeners.Remove(progress);
        }

        private async UniTaskVoid RunAsync()
        {
            Exception failure = null;
            try
            {
                _handle = _start();
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
            }
            catch (Exception exception)
            {
                failure = exception is ContentOperationException
                    ? exception
                    : new ContentOperationException("Download", _key, exception);
            }
            finally
            {
                if (_handle.IsValid())
                    UnityEngine.AddressableAssets.Addressables.Release(_handle);

                _onCompleted?.Invoke();
            }

            // Remove the operation before resuming callers that might retry synchronously.
            if (failure == null)
                _completion.TrySetResult(true);
            else
                _completion.TrySetException(failure);
        }

        private void ReportProgress(long downloadedBytes, long totalBytes, bool isDone)
        {
            var progress = new ContentDownloadProgress(downloadedBytes, totalBytes, isDone);
            _lastProgress = progress;

            // A callback can cancel a request and remove a listener during reporting.
            foreach (var listener in _progressListeners.ToArray())
                ReportTo(listener, progress);
        }

        private static void ReportTo(IProgress<ContentDownloadProgress> listener, ContentDownloadProgress progress)
        {
            try
            {
                listener.Report(progress);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }
    }
}

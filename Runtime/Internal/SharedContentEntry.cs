using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace UContent.Internal
{
    internal interface ISharedContentEntry { }

    internal sealed class SharedContentEntry<T> : ISharedContentEntry where T : UnityEngine.Object
    {
        private readonly object _key;
        private readonly Action _onReleased;
        private readonly UniTaskCompletionSource<T> _completion = new();

        private AsyncOperationHandle<T> _handle;

        private int _references;
        private bool _started;
        private bool _completed;
        private bool _released;

        public UniTask<T> Task => _completion.Task;
        public int References => _references;

        public SharedContentEntry(object key, Action onReleased)
        {
            _key = key;
            _onReleased = onReleased;
        }

        public void Retain()
        {
            if (_released)
                throw new ObjectDisposedException(nameof(SharedContentEntry<T>));

            _references++;
        }

        public void Start()
        {
            if (_started)
                return;

            _started = true;
            RunAsync().Forget();
        }

        public void Release()
        {
            if (_released)
                return;

            if (_references > 0)
                _references--;

            TryRelease();
        }

        private async UniTaskVoid RunAsync()
        {
            _handle = Addressables.LoadAssetAsync<T>(_key);

            try
            {
                var asset = await _handle.ToUniTask();

                if (_handle.Status != AsyncOperationStatus.Succeeded || asset == null)
                {
                    Fail(new ContentOperationException("Load", _key, _handle.OperationException));
                    return;
                }

                _completed = true;
                _completion.TrySetResult(asset);

                TryRelease();
            }
            catch (Exception exception)
            {
                if (exception is ContentOperationException)
                    Fail(exception);
                else
                    Fail(new ContentOperationException("Load", _key, exception));
            }
        }

        private void Fail(Exception exception)
        {
            _completed = true;
            _completion.TrySetException(exception);
            ReleaseHandle();
        }

        private void TryRelease()
        {
            if (!_completed || _references > 0)
                return;

            ReleaseHandle();
        }

        private void ReleaseHandle()
        {
            if (_released)
                return;

            _released = true;

            if (_handle.IsValid())
                Addressables.Release(_handle);

            _onReleased?.Invoke();
        }
    }
}
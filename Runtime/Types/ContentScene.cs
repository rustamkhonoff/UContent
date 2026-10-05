using System;
using Cysharp.Threading.Tasks;
using UContent.Diagnostics;
using UnityEngine.SceneManagement;

namespace UContent
{
    public sealed class ContentScene
    {
        private readonly Func<UniTask> _activate;
        private readonly int _debugId;
        private Func<UniTask> _unload;
        private UniTaskCompletionSource _activationCompletion;
        private UniTaskCompletionSource _unloadCompletion;

        public object Key { get; }
        public Scene Scene { get; }
        public bool IsActivated { get; private set; }
        public bool IsUnloaded => _unload == null;

        internal ContentScene(object key, Scene scene, Func<UniTask> activate, Func<UniTask> unload, bool isActivated)
        {
            Key = key;
            Scene = scene;
            _activate = activate ?? throw new ArgumentNullException(nameof(activate));
            _unload = unload ?? throw new ArgumentNullException(nameof(unload));
            IsActivated = isActivated;
            _debugId = ContentDiagnostics.Register(ContentDebugType.Scene, key);
        }

        public UniTask ActivateAsync()
        {
            if (IsUnloaded)
                throw new ObjectDisposedException(nameof(ContentScene));

            if (IsActivated)
                return UniTask.CompletedTask;

            if (_activationCompletion != null)
                return _activationCompletion.Task;

            var completion = new UniTaskCompletionSource();
            _activationCompletion = completion;
            ActivateCoreAsync(completion).Forget();
            return completion.Task;
        }

        public UniTask UnloadAsync()
        {
            if (IsUnloaded)
                return UniTask.CompletedTask;

            if (_unloadCompletion != null)
                return _unloadCompletion.Task;

            var completion = new UniTaskCompletionSource();
            _unloadCompletion = completion;
            UnloadCoreAsync(completion).Forget();
            return completion.Task;
        }

        private async UniTaskVoid ActivateCoreAsync(UniTaskCompletionSource completion)
        {
            try
            {
                await _activate();
                IsActivated = true;
                completion.TrySetResult();
            }
            catch (Exception exception)
            {
                _activationCompletion = null;
                completion.TrySetException(exception);
            }
        }

        private async UniTaskVoid UnloadCoreAsync(UniTaskCompletionSource completion)
        {
            try
            {
                // Unblock Unity's operation queue before unloading a pending inactive scene.
                await ActivateAsync();
                await _unload();
                _unload = null;
                ContentDiagnostics.Release(_debugId);
                completion.TrySetResult();
            }
            catch (Exception exception)
            {
                _unloadCompletion = null;
                completion.TrySetException(exception);
            }
        }
    }
}

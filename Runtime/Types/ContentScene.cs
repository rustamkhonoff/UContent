using System;
using Cysharp.Threading.Tasks;
using UnityEngine.SceneManagement;

namespace UContent
{
    public sealed class ContentScene
    {
        private Func<UniTask> _unload;

        public object Key { get; }
        public Scene Scene { get; }
        public bool IsUnloaded => _unload == null;

        internal ContentScene(object key, Scene scene, Func<UniTask> unload)
        {
            Key = key;
            Scene = scene;
            _unload = unload ?? throw new ArgumentNullException(nameof(unload));
        }

        public async UniTask UnloadAsync()
        {
            var unload = _unload;

            if (unload == null)
                return;

            _unload = null;

            try
            {
                await unload();
            }
            catch
            {
                _unload = unload;
                throw;
            }
        }
    }
}
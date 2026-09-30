using System;
using Cysharp.Threading.Tasks;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;

namespace UContent
{
    public sealed class ContentScene
    {
        private Func<UniTask> m_unload;

        public object Key { get; }
        public Scene Scene { get; }
        public bool IsUnloaded => m_unload == null;

        internal ContentScene(object key, Scene scene, Func<UniTask> unload)
        {
            Key = key;
            Scene = scene;
            m_unload = unload ?? throw new ArgumentNullException(nameof(unload));
        }

        public async UniTask UnloadAsync()
        {
            var unload = m_unload;

            if (unload == null)
                return;

            m_unload = null;

            try
            {
                await unload();
            }
            catch
            {
                m_unload = unload;
                throw;
            }
        }
    }
}
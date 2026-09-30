using System;
using Cysharp.Threading.Tasks;
using UContent.Diagnostics;
using UnityEngine.SceneManagement;

namespace UContent
{
    public sealed class ContentScene
    {
        private readonly int m_debugId;

        private Func<UniTask> m_unload;

        public object Key { get; }
        public Scene Scene { get; }
        public bool IsUnloaded => m_unload == null;

        internal ContentScene(object key, Scene scene, Func<UniTask> unload)
        {
            Key = key;
            Scene = scene;
            m_unload = unload ?? throw new ArgumentNullException(nameof(unload));
            m_debugId = ContentDiagnostics.Register(ContentDebugType.Scene, key, scene.name);
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
                ContentDiagnostics.Release(m_debugId);
            }
            catch
            {
                m_unload = unload;
                throw;
            }
        }
    }
}
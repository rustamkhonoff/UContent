using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;

namespace UContent
{
    public sealed class AddressablesContentService : IContentService
    {
        public async UniTask InitializeAsync(CancellationToken cancellationToken = default)
        {
            var handle = Addressables.InitializeAsync(false);

            try
            {
                await handle.ToUniTask(cancellationToken: cancellationToken);

                if (handle.Status != AsyncOperationStatus.Succeeded)
                    throw CreateException("Initialize", null, handle.OperationException);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (ContentOperationException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw CreateException("Initialize", null, exception);
            }
            finally
            {
                Release(handle);
            }
        }

        public async UniTask<ContentHandle<T>> LoadAsync<T>(object key, CancellationToken cancellationToken = default) where T : UnityEngine.Object
        {
            var handle = Addressables.LoadAssetAsync<T>(key);

            try
            {
                var asset = await handle.ToUniTask(cancellationToken: cancellationToken);

                if (handle.Status != AsyncOperationStatus.Succeeded || asset == null)
                    throw CreateException("Load", key, handle.OperationException);

                return new ContentHandle<T>(key, asset, () => Release(handle));
            }
            catch (OperationCanceledException)
            {
                Release(handle);
                throw;
            }
            catch (ContentOperationException)
            {
                Release(handle);
                throw;
            }
            catch (Exception exception)
            {
                Release(handle);
                throw CreateException("Load", key, exception);
            }
        }

        public async UniTask<ContentHandle<IReadOnlyList<T>>> LoadAllAsync<T>(object key, CancellationToken cancellationToken = default) where T : UnityEngine.Object
        {
            var handle = Addressables.LoadAssetsAsync<T>(key, null, true);

            try
            {
                var assets = await handle.ToUniTask(cancellationToken: cancellationToken);

                if (handle.Status != AsyncOperationStatus.Succeeded || assets == null)
                    throw CreateException("LoadAll", key, handle.OperationException);

                IReadOnlyList<T> result = new ReadOnlyCollection<T>(assets);
                return new ContentHandle<IReadOnlyList<T>>(key, result, () => Release(handle));
            }
            catch (OperationCanceledException)
            {
                Release(handle);
                throw;
            }
            catch (ContentOperationException)
            {
                Release(handle);
                throw;
            }
            catch (Exception exception)
            {
                Release(handle);
                throw CreateException("LoadAll", key, exception);
            }
        }

        public async UniTask<ContentInstance> InstantiateAsync(object key, Transform parent = null, bool instantiateInWorldSpace = false, CancellationToken cancellationToken = default)
        {
            var handle = Addressables.InstantiateAsync(key, parent, instantiateInWorldSpace, false);

            try
            {
                var instance = await handle.ToUniTask(cancellationToken: cancellationToken);

                if (handle.Status != AsyncOperationStatus.Succeeded || instance == null)
                    throw CreateException("Instantiate", key, handle.OperationException);

                return new ContentInstance(key, instance, () => ReleaseInstance(handle));
            }
            catch (OperationCanceledException)
            {
                ReleaseInstanceAfterCompletion(handle);
                throw;
            }
            catch (ContentOperationException)
            {
                ReleaseInstanceAfterCompletion(handle);
                throw;
            }
            catch (Exception exception)
            {
                ReleaseInstanceAfterCompletion(handle);
                throw CreateException("Instantiate", key, exception);
            }
        }

        public async UniTask<ContentInstance> InstantiateAsync(object key, Vector3 position, Quaternion rotation, Transform parent = null, CancellationToken cancellationToken = default)
        {
            var handle = Addressables.InstantiateAsync(key, position, rotation, parent, false);

            try
            {
                var instance = await handle.ToUniTask(cancellationToken: cancellationToken);

                if (handle.Status != AsyncOperationStatus.Succeeded || instance == null)
                    throw CreateException("Instantiate", key, handle.OperationException);

                return new ContentInstance(key, instance, () => ReleaseInstance(handle));
            }
            catch (OperationCanceledException)
            {
                ReleaseInstanceAfterCompletion(handle);
                throw;
            }
            catch (ContentOperationException)
            {
                ReleaseInstanceAfterCompletion(handle);
                throw;
            }
            catch (Exception exception)
            {
                ReleaseInstanceAfterCompletion(handle);
                throw CreateException("Instantiate", key, exception);
            }
        }

        public async UniTask<ContentScene> LoadSceneAsync(object key, LoadSceneMode mode = LoadSceneMode.Single, bool activateOnLoad = true, int priority = 100)
        {
            var handle = Addressables.LoadSceneAsync(key, mode, activateOnLoad, priority);

            try
            {
                await handle;

                if (handle.Status != AsyncOperationStatus.Succeeded)
                    throw CreateException("LoadScene", key, handle.OperationException);

                return new ContentScene(key, handle.Result.Scene, () => UnloadSceneAsync(handle, key));
            }
            catch (ContentOperationException)
            {
                Release(handle);
                throw;
            }
            catch (Exception exception)
            {
                Release(handle);
                throw CreateException("LoadScene", key, exception);
            }
        }

        public async UniTask<long> GetDownloadSizeAsync(object key, CancellationToken cancellationToken = default)
        {
            var handle = Addressables.GetDownloadSizeAsync(key);

            try
            {
                var size = await handle.ToUniTask(cancellationToken: cancellationToken);

                if (handle.Status != AsyncOperationStatus.Succeeded)
                    throw CreateException("GetDownloadSize", key, handle.OperationException);

                return size;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (ContentOperationException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw CreateException("GetDownloadSize", key, exception);
            }
            finally
            {
                Release(handle);
            }
        }

        public async UniTask DownloadAsync(object key, IProgress<ContentDownloadProgress> progress = null, CancellationToken cancellationToken = default)
        {
            var handle = Addressables.DownloadDependenciesAsync(key, false);

            try
            {
                long lastDownloadedBytes = -1;
                long lastTotalBytes = -1;

                while (!handle.IsDone)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var status = handle.GetDownloadStatus();

                    if (status.DownloadedBytes != lastDownloadedBytes || status.TotalBytes != lastTotalBytes)
                    {
                        progress?.Report(new ContentDownloadProgress(status.DownloadedBytes, status.TotalBytes, false));
                        lastDownloadedBytes = status.DownloadedBytes;
                        lastTotalBytes = status.TotalBytes;
                    }

                    await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
                }

                if (handle.Status != AsyncOperationStatus.Succeeded)
                    throw CreateException("Download", key, handle.OperationException);

                var finalStatus = handle.GetDownloadStatus();
                progress?.Report(new ContentDownloadProgress(finalStatus.DownloadedBytes, finalStatus.TotalBytes, true));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (ContentOperationException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw CreateException("Download", key, exception);
            }
            finally
            {
                Release(handle);
            }
        }

        public async UniTask<bool> ClearCacheAsync(object key, CancellationToken cancellationToken = default)
        {
            var handle = Addressables.ClearDependencyCacheAsync(key, false);

            try
            {
                var result = await handle.ToUniTask(cancellationToken: cancellationToken);

                if (handle.Status != AsyncOperationStatus.Succeeded)
                    throw CreateException("ClearCache", key, handle.OperationException);

                return result;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (ContentOperationException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw CreateException("ClearCache", key, exception);
            }
            finally
            {
                Release(handle);
            }
        }

        public async UniTask<bool> CleanBundleCacheAsync(CancellationToken cancellationToken = default)
        {
            var handle = Addressables.CleanBundleCache();

            try
            {
                var result = await handle.ToUniTask(cancellationToken: cancellationToken);

                if (handle.Status != AsyncOperationStatus.Succeeded)
                    throw CreateException("CleanBundleCache", null, handle.OperationException);

                return result;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (ContentOperationException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw CreateException("CleanBundleCache", null, exception);
            }
            finally
            {
                Release(handle);
            }
        }

        public ContentScope CreateScope(string name = null)
        {
            return new ContentScope(this, name);
        }

        private static async UniTask UnloadSceneAsync(AsyncOperationHandle<SceneInstance> sceneHandle, object key)
        {
            if (!sceneHandle.IsValid())
                return;

            var unloadHandle = Addressables.UnloadSceneAsync(sceneHandle, false);

            try
            {
                await unloadHandle;

                if (unloadHandle.Status != AsyncOperationStatus.Succeeded)
                    throw CreateException("UnloadScene", key, unloadHandle.OperationException);
            }
            finally
            {
                Release(unloadHandle);
            }
        }

        private static void ReleaseInstance(AsyncOperationHandle<GameObject> handle)
        {
            if (!handle.IsValid())
                return;

            if (handle.Status == AsyncOperationStatus.Succeeded)
            {
                Addressables.ReleaseInstance(handle);
                return;
            }

            Addressables.Release(handle);
        }

        private static void ReleaseInstanceAfterCompletion(AsyncOperationHandle<GameObject> handle)
        {
            if (!handle.IsValid())
                return;

            if (handle.IsDone)
            {
                ReleaseInstance(handle);
                return;
            }

            handle.Completed += ReleaseCompletedInstance;
        }

        private static void ReleaseCompletedInstance(AsyncOperationHandle<GameObject> handle)
        {
            ReleaseInstance(handle);
        }

        private static void Release<T>(AsyncOperationHandle<T> handle)
        {
            if (handle.IsValid())
                Addressables.Release(handle);
        }

        private static void Release(AsyncOperationHandle handle)
        {
            if (handle.IsValid())
                Addressables.Release(handle);
        }

        private static ContentOperationException CreateException(string operation, object key, Exception innerException)
        {
            return new ContentOperationException(operation, key, innerException);
        }
    }
}
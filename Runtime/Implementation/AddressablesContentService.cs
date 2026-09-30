using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
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
        private readonly Dictionary<ContentRequestKey, ISharedContentEntry> m_sharedLoads = new();

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
            if (key == null)
                throw new ArgumentNullException(nameof(key));

            var requestKey = new ContentRequestKey(typeof(T), key);
            var entry = GetOrCreateSharedEntry<T>(requestKey, key);

            entry.Retain();

            try
            {
                var asset = await entry.Task.AttachExternalCancellation(cancellationToken);
                return new ContentHandle<T>(key, asset, entry.Release);
            }
            catch
            {
                entry.Release();
                throw;
            }
        }

        public async UniTask<ContentHandle<IReadOnlyList<T>>> LoadAllAsync<T>(object key, CancellationToken cancellationToken = default) where T : UnityEngine.Object
        {
            var handle = Addressables.LoadAssetsAsync<T>(key, null, false);

            try
            {
                var assets = await handle.ToUniTask(cancellationToken: cancellationToken);

                if (handle.Status != AsyncOperationStatus.Succeeded || assets == null)
                    throw CreateException("LoadAll", key, handle.OperationException);

                IReadOnlyList<T> result = new ReadOnlyCollection<T>(assets);
                return new ContentHandle<IReadOnlyList<T>>(key, result, () => Release(handle));
            }
            catch
            {
                Release(handle);
                throw;
            }
        }

        public async UniTask<ContentHandle<IReadOnlyList<T>>> LoadAllAsync<T>(IEnumerable<object> keys, ContentMergeMode mergeMode = ContentMergeMode.Union, CancellationToken cancellationToken = default) where T : UnityEngine.Object
        {
            var keyList = keys?.ToList();

            if (keyList == null || keyList.Count == 0)
                throw new ArgumentException("Content keys cannot be empty.", nameof(keys));

            var handle = Addressables.LoadAssetsAsync<T>(keyList, null, mergeMode.ToAddressables(), false);

            try
            {
                var assets = await handle.ToUniTask(cancellationToken: cancellationToken);

                if (handle.Status != AsyncOperationStatus.Succeeded || assets == null)
                    throw CreateException("LoadAll", string.Join(", ", keyList), handle.OperationException);

                IReadOnlyList<T> result = new ReadOnlyCollection<T>(assets);
                return new ContentHandle<IReadOnlyList<T>>(keyList, result, () => Release(handle));
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
                throw CreateException("LoadAll", string.Join(", ", keyList), exception);
            }
        }

        public async UniTask<bool> ExistsAsync<T>(object key, CancellationToken cancellationToken = default) where T : UnityEngine.Object
        {
            var handle = Addressables.LoadResourceLocationsAsync(key, typeof(T));

            try
            {
                var locations = await handle.ToUniTask(cancellationToken: cancellationToken);
                return locations != null && locations.Count > 0;
            }
            finally
            {
                Release(handle);
            }
        }

        public async UniTask<ContentInstance> InstantiateAsync(object key, Transform parent = null, bool instantiateInWorldSpace = false, CancellationToken cancellationToken = default)
        {
            var handle = Addressables.InstantiateAsync(key, parent, instantiateInWorldSpace, false);

            try
            {
                GameObject instance = await handle.ToUniTask(cancellationToken: cancellationToken);

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
                GameObject instance = await handle.ToUniTask(cancellationToken: cancellationToken);

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
                long size = await handle.ToUniTask(cancellationToken: cancellationToken);

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

        public async UniTask<long> GetDownloadSizeAsync(IEnumerable<object> keys, CancellationToken cancellationToken = default)
        {
            var keyList = keys?.ToList();

            if (keyList == null || keyList.Count == 0)
                return 0;

            var handle = Addressables.GetDownloadSizeAsync(keyList);

            try
            {
                long size = await handle.ToUniTask(cancellationToken: cancellationToken);

                if (handle.Status != AsyncOperationStatus.Succeeded)
                    throw CreateException("GetDownloadSize", string.Join(", ", keyList), handle.OperationException);

                return size;
            }
            finally
            {
                Release(handle);
            }
        }

        public async UniTask DownloadAsync(object key, IProgress<ContentDownloadProgress> progress = null, CancellationToken cancellationToken = default)
        {
            AsyncOperationHandle handle = Addressables.DownloadDependenciesAsync(key, false);

            try
            {
                long lastDownloadedBytes = -1;
                long lastTotalBytes = -1;

                while (!handle.IsDone)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    DownloadStatus status = handle.GetDownloadStatus();

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

                DownloadStatus finalStatus = handle.GetDownloadStatus();
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

        public async UniTask DownloadAsync(IEnumerable<object> keys, ContentMergeMode mergeMode = ContentMergeMode.Union, IProgress<ContentDownloadProgress> progress = null, CancellationToken cancellationToken = default)
        {
            var keyList = keys?.ToList();

            if (keyList == null || keyList.Count == 0)
                return;

            AsyncOperationHandle handle = Addressables.DownloadDependenciesAsync(keyList, mergeMode.ToAddressables(), false);

            try
            {
                while (!handle.IsDone)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    DownloadStatus status = handle.GetDownloadStatus();
                    progress?.Report(new ContentDownloadProgress(status.DownloadedBytes, status.TotalBytes, false));

                    await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
                }

                if (handle.Status != AsyncOperationStatus.Succeeded)
                    throw CreateException("Download", string.Join(", ", keyList), handle.OperationException);

                DownloadStatus finalStatus = handle.GetDownloadStatus();
                progress?.Report(new ContentDownloadProgress(finalStatus.DownloadedBytes, finalStatus.TotalBytes, true));
            }
            finally
            {
                Release(handle);
            }
        }

        public async UniTask<IReadOnlyList<string>> CheckForCatalogUpdatesAsync(CancellationToken cancellationToken = default)
        {
            var handle = Addressables.CheckForCatalogUpdates(false);

            try
            {
                var catalogs = await handle.ToUniTask(cancellationToken: cancellationToken);

                if (handle.Status != AsyncOperationStatus.Succeeded)
                    throw CreateException("CheckForCatalogUpdates", null, handle.OperationException);

                return (IReadOnlyList<string>)catalogs ?? Array.Empty<string>();
            }
            finally
            {
                Release(handle);
            }
        }

        public async UniTask<int> UpdateCatalogsAsync(IEnumerable<string> catalogs = null, bool cleanBundleCache = true, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var catalogList = catalogs?.ToList();
            var handle = Addressables.UpdateCatalogs(cleanBundleCache, catalogList, false);

            try
            {
                var locators = await handle.ToUniTask();

                if (handle.Status != AsyncOperationStatus.Succeeded)
                    throw CreateException("UpdateCatalogs", null, handle.OperationException);

                return locators?.Count ?? 0;
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
                bool result = await handle.ToUniTask(cancellationToken: cancellationToken);

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
                bool result = await handle.ToUniTask(cancellationToken: cancellationToken);

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

        private SharedContentEntry<T> GetOrCreateSharedEntry<T>(ContentRequestKey requestKey, object key) where T : UnityEngine.Object
        {
            if (m_sharedLoads.TryGetValue(requestKey, out var existing))
                return (SharedContentEntry<T>)existing;

            var entry = new SharedContentEntry<T>(key, () => m_sharedLoads.Remove(requestKey));

            m_sharedLoads.Add(requestKey, entry);
            entry.Start();

            return entry;
        }
    }
}
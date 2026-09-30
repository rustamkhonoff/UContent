using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using UContent.Internal;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;

namespace UContent
{
    public sealed class AddressablesContentService : IContentService
    {
        private readonly Dictionary<ContentRequestKey, ISharedContentEntry> _sharedLoads = new();
        private readonly Dictionary<ContentDownloadKey, SharedDownloadOperation> _sharedDownloads = new();

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
            if (key == null)
                throw new ArgumentNullException(nameof(key));

            var handle = Addressables.LoadAssetsAsync<T>(key, null, false);

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

        public async UniTask<ContentHandle<IReadOnlyList<T>>> LoadAllAsync<T>(IEnumerable<object> keys, ContentMergeMode mergeMode = ContentMergeMode.Union, CancellationToken cancellationToken = default) where T : UnityEngine.Object
        {
            var keyList = ToKeyList(keys);

            if (keyList.Count == 0)
                throw new ArgumentException("Content keys cannot be empty.", nameof(keys));

            var handle = Addressables.LoadAssetsAsync<T>(keyList, null, mergeMode.ToAddressables(), false);
            var debugKey = string.Join(", ", keyList);

            try
            {
                var assets = await handle.ToUniTask(cancellationToken: cancellationToken);

                if (handle.Status != AsyncOperationStatus.Succeeded || assets == null)
                    throw CreateException("LoadAll", debugKey, handle.OperationException);

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
                throw CreateException("LoadAll", debugKey, exception);
            }
        }

        public async UniTask<bool> ExistsAsync<T>(object key, CancellationToken cancellationToken = default) where T : UnityEngine.Object
        {
            if (key == null)
                return false;

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
            if (key == null)
                throw new ArgumentNullException(nameof(key));

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
            if (key == null)
                throw new ArgumentNullException(nameof(key));

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
            if (key == null)
                throw new ArgumentNullException(nameof(key));

            var handle = Addressables.LoadSceneAsync(key, mode, activateOnLoad, priority);

            try
            {
                var sceneInstance = await handle.ToUniTask();

                if (handle.Status != AsyncOperationStatus.Succeeded)
                    throw CreateException("LoadScene", key, handle.OperationException);

                return new ContentScene(key, sceneInstance.Scene, () => UnloadSceneAsync(handle, key));
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
            if (key == null)
                return 0;

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

        public async UniTask<long> GetDownloadSizeAsync(IEnumerable<object> keys, CancellationToken cancellationToken = default)
        {
            var keyList = ToKeyList(keys);

            if (keyList.Count == 0)
                return 0;

            var handle = Addressables.GetDownloadSizeAsync(keyList);

            try
            {
                var size = await handle.ToUniTask(cancellationToken: cancellationToken);

                if (handle.Status != AsyncOperationStatus.Succeeded)
                    throw CreateException("GetDownloadSize", string.Join(", ", keyList), handle.OperationException);

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
                throw CreateException("GetDownloadSize", string.Join(", ", keyList), exception);
            }
            finally
            {
                Release(handle);
            }
        }

        public UniTask DownloadAsync(object key, IProgress<ContentDownloadProgress> progress = null, CancellationToken cancellationToken = default)
        {
            if (key == null)
                throw new ArgumentNullException(nameof(key));

            var requestKey = ContentDownloadKey.Single(key);
            return DownloadSharedAsync(requestKey, key, () => Addressables.DownloadDependenciesAsync(key, false), progress, cancellationToken);
        }

        public UniTask DownloadAsync(IEnumerable<object> keys, ContentMergeMode mergeMode = ContentMergeMode.Union, IProgress<ContentDownloadProgress> progress = null, CancellationToken cancellationToken = default)
        {
            var keyList = ToKeyList(keys);

            if (keyList.Count == 0)
                return UniTask.CompletedTask;

            var requestKey = ContentDownloadKey.Multiple(keyList, mergeMode);
            var debugKey = string.Join(", ", keyList);

            return DownloadSharedAsync(requestKey, debugKey, () => Addressables.DownloadDependenciesAsync(keyList, mergeMode.ToAddressables(), false), progress, cancellationToken);
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
                throw CreateException("CheckForCatalogUpdates", null, exception);
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
            catch (ContentOperationException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw CreateException("UpdateCatalogs", null, exception);
            }
            finally
            {
                Release(handle);
            }
        }

        public async UniTask<bool> ClearCacheAsync(object key, CancellationToken cancellationToken = default)
        {
            if (key == null)
                return false;

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

        private SharedContentEntry<T> GetOrCreateSharedEntry<T>(ContentRequestKey requestKey, object key) where T : UnityEngine.Object
        {
            if (_sharedLoads.TryGetValue(requestKey, out var existing))
                return (SharedContentEntry<T>)existing;

            var entry = new SharedContentEntry<T>(key, () => _sharedLoads.Remove(requestKey));

            _sharedLoads.Add(requestKey, entry);
            entry.Start();

            return entry;
        }

        private async UniTask DownloadSharedAsync(ContentDownloadKey requestKey, object debugKey, Func<AsyncOperationHandle> start, IProgress<ContentDownloadProgress> progress, CancellationToken cancellationToken)
        {
            if (!_sharedDownloads.TryGetValue(requestKey, out var operation))
            {
                operation = new SharedDownloadOperation(debugKey, start, () => _sharedDownloads.Remove(requestKey));

                _sharedDownloads.Add(requestKey, operation);
                operation.Start();
            }

            operation.AddProgress(progress);

            try
            {
                await operation.Task.AttachExternalCancellation(cancellationToken);
            }
            finally
            {
                operation.RemoveProgress(progress);
            }
        }

        private static async UniTask UnloadSceneAsync(AsyncOperationHandle<SceneInstance> sceneHandle, object key)
        {
            if (!sceneHandle.IsValid())
                return;

            var unloadHandle = Addressables.UnloadSceneAsync(sceneHandle, false);

            try
            {
                await unloadHandle.ToUniTask();

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

        private static List<object> ToKeyList(IEnumerable<object> keys)
        {
            return keys?.Where(x => x != null).ToList() ?? new List<object>();
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
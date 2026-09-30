using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UContent
{
    public interface IContentService
    {
        UniTask InitializeAsync(CancellationToken cancellationToken = default);

        UniTask<ContentHandle<T>> LoadAsync<T>(object key, CancellationToken cancellationToken = default) where T : UnityEngine.Object;
        UniTask<ContentHandle<IReadOnlyList<T>>> LoadAllAsync<T>(object key, CancellationToken cancellationToken = default) where T : UnityEngine.Object;
        UniTask<ContentHandle<IReadOnlyList<T>>> LoadAllAsync<T>(IEnumerable<object> keys, ContentMergeMode mergeMode = ContentMergeMode.Union, CancellationToken cancellationToken = default) where T : UnityEngine.Object;

        UniTask<bool> ExistsAsync<T>(object key, CancellationToken cancellationToken = default) where T : UnityEngine.Object;

        UniTask<ContentInstance> InstantiateAsync(object key, Transform parent = null, bool instantiateInWorldSpace = false, CancellationToken cancellationToken = default);
        UniTask<ContentInstance> InstantiateAsync(object key, Vector3 position, Quaternion rotation, Transform parent = null, CancellationToken cancellationToken = default);

        UniTask<ContentScene> LoadSceneAsync(object key, LoadSceneMode mode = LoadSceneMode.Single, bool activateOnLoad = true, int priority = 100);

        UniTask<long> GetDownloadSizeAsync(object key, CancellationToken cancellationToken = default);
        UniTask<long> GetDownloadSizeAsync(IEnumerable<object> keys, CancellationToken cancellationToken = default);

        UniTask DownloadAsync(object key, IProgress<ContentDownloadProgress> progress = null, CancellationToken cancellationToken = default);
        UniTask DownloadAsync(IEnumerable<object> keys, ContentMergeMode mergeMode = ContentMergeMode.Union, IProgress<ContentDownloadProgress> progress = null, CancellationToken cancellationToken = default);

        UniTask<IReadOnlyList<string>> CheckForCatalogUpdatesAsync(CancellationToken cancellationToken = default);
        UniTask<int> UpdateCatalogsAsync(IEnumerable<string> catalogs = null, bool cleanBundleCache = true, CancellationToken cancellationToken = default);

        UniTask<bool> ClearCacheAsync(object key, CancellationToken cancellationToken = default);
        UniTask<bool> CleanBundleCacheAsync(CancellationToken cancellationToken = default);

        ContentScope CreateScope(string name = null);
    }
}
// Deterministic engine/Addressables stand-ins; no Unity native runtime is used here.
using Cysharp.Threading.Tasks;
namespace UnityEngine
{
    public class Object { }
    public class Transform : Object { }
    public class GameObject : Object { public Transform transform = new(); }
    public struct Vector3 { }
    public struct Quaternion { }
    public class AsyncOperation { public UniTask Task = UniTask.CompletedTask; }
    public static class Debug
    {
        public static bool isDebugBuild = true;
        public static readonly List<Exception> Logged = new();
        public static void LogException(Exception ex) => Logged.Add(ex);
    }
}
namespace UnityEngine.SceneManagement
{
    public enum LoadSceneMode { Single, Additive }
    public struct Scene { }
}
namespace UnityEngine.ResourceManagement.AsyncOperations
{
    public enum AsyncOperationStatus { None, Succeeded, Failed }
    public struct DownloadStatus { public long DownloadedBytes, TotalBytes; }
    public class Operation
    {
        public readonly TaskCompletionSource<object> Completion = new();
        public bool Valid = true;
        public int Releases;
        public DownloadStatus Download = new() { DownloadedBytes = 100, TotalBytes = 100 };
        public void Succeed(object value) => Completion.TrySetResult(value);
        public void Fail(Exception ex) => Completion.TrySetException(ex);
    }
    public struct AsyncOperationHandle
    {
        public Operation Operation;
        public bool IsValid() => Operation != null && Operation.Valid;
        public bool IsDone => Operation.Completion.Task.IsCompleted;
        public AsyncOperationStatus Status => Operation.Completion.Task.IsFaulted ? AsyncOperationStatus.Failed : IsDone ? AsyncOperationStatus.Succeeded : AsyncOperationStatus.None;
        public Exception OperationException => Operation.Completion.Task.Exception?.InnerException;
        public DownloadStatus GetDownloadStatus() => Operation.Download;
    }
    public struct AsyncOperationHandle<T>
    {
        public Operation Operation;
        public bool IsValid() => Operation != null && Operation.Valid;
        public bool IsDone => Operation.Completion.Task.IsCompleted;
        public AsyncOperationStatus Status => Operation.Completion.Task.IsFaulted ? AsyncOperationStatus.Failed : IsDone ? AsyncOperationStatus.Succeeded : AsyncOperationStatus.None;
        public Exception OperationException => Operation.Completion.Task.Exception?.InnerException;
        public event Action<AsyncOperationHandle<T>> Completed
        {
            add { var copy = this; Operation.Completion.Task.GetAwaiter().OnCompleted(() => value(copy)); }
            remove { }
        }
    }
}
namespace UnityEngine.ResourceManagement.ResourceProviders
{
    public struct SceneInstance
    {
        public SceneManagement.Scene Scene;
        public Func<AsyncOperation> Activate;
        public AsyncOperation ActivateAsync() => Activate();
    }
}
namespace UnityEngine.AddressableAssets
{
    using ResourceManagement.AsyncOperations;
    using ResourceManagement.ResourceProviders;
    using SceneManagement;
    public static class Addressables
    {
        public enum MergeMode { UseFirst, Union, Intersection }
        public static Func<object, Type, Operation> Load;
        public static Func<object, Operation> Download;
        public static Func<object, Operation> Instantiate;
        public static Func<object, bool, Operation> LoadScene;
        public static Func<Operation> UnloadScene;
        public static int LoadCalls, DownloadCalls;
        private static AsyncOperationHandle<T> Done<T>(T value)
        {
            var op = new Operation(); op.Succeed(value); return new() { Operation = op };
        }
        public static AsyncOperationHandle<object> InitializeAsync(bool autoRelease) => Done(new object());
        public static AsyncOperationHandle<T> LoadAssetAsync<T>(object key)
        { LoadCalls++; return new() { Operation = Load(key, typeof(T)) }; }
        public static AsyncOperationHandle<IList<T>> LoadAssetsAsync<T>(object key, object callback, bool release) => Done<IList<T>>(new List<T>());
        public static AsyncOperationHandle<IList<T>> LoadAssetsAsync<T>(IEnumerable<object> keys, object callback, MergeMode mode, bool release) => Done<IList<T>>(new List<T>());
        public static AsyncOperationHandle<IList<object>> LoadResourceLocationsAsync(object key, Type type) => Done<IList<object>>(new List<object>());
        public static AsyncOperationHandle<GameObject> InstantiateAsync(object key, Transform parent, bool world, bool track) => new() { Operation = Instantiate(key) };
        public static AsyncOperationHandle<GameObject> InstantiateAsync(object key, Vector3 p, Quaternion r, Transform parent, bool track) => new() { Operation = Instantiate(key) };
        public static AsyncOperationHandle<SceneInstance> LoadSceneAsync(object key, LoadSceneMode mode, bool activate, int priority) => new() { Operation = LoadScene(key, activate) };
        public static AsyncOperationHandle<SceneInstance> UnloadSceneAsync(AsyncOperationHandle<SceneInstance> scene, bool autoRelease) => new() { Operation = UnloadScene() };
        public static AsyncOperationHandle<long> GetDownloadSizeAsync(object key) => Done(0L);
        public static AsyncOperationHandle DownloadDependenciesAsync(object key, bool autoRelease)
        { DownloadCalls++; return new() { Operation = Download(key) }; }
        public static AsyncOperationHandle DownloadDependenciesAsync(IEnumerable<object> keys, MergeMode mode, bool autoRelease) => DownloadDependenciesAsync(keys, autoRelease);
        public static AsyncOperationHandle<List<string>> CheckForCatalogUpdates(bool autoRelease) => Done(new List<string>());
        public static AsyncOperationHandle<List<object>> UpdateCatalogs(bool clean, IEnumerable<string> catalogs, bool autoRelease) => Done(new List<object>());
        public static AsyncOperationHandle<bool> ClearDependencyCacheAsync(object key, bool autoRelease) => Done(true);
        public static AsyncOperationHandle<bool> CleanBundleCache() => Done(true);
        public static void Release<T>(AsyncOperationHandle<T> handle) => Release(new AsyncOperationHandle { Operation = handle.Operation });
        public static void Release(AsyncOperationHandle handle)
        { handle.Operation.Releases++; handle.Operation.Valid = false; }
        public static void ReleaseInstance(AsyncOperationHandle<GameObject> handle) => Release(handle);
    }
}
namespace Cysharp.Threading.Tasks
{
    using UnityEngine.ResourceManagement.AsyncOperations;
    public static class EngineAwaitAdapter
    {
        public static async UniTask<T> ToUniTask<T>(this AsyncOperationHandle<T> handle, CancellationToken cancellationToken = default)
        { return (T)await handle.Operation.Completion.Task.WaitAsync(cancellationToken); }
        public static async UniTask ToUniTask(this AsyncOperationHandle handle, CancellationToken cancellationToken = default)
        { await handle.Operation.Completion.Task.WaitAsync(cancellationToken); }
        public static UniTask ToUniTask(this UnityEngine.AsyncOperation operation) => operation.Task;
    }
}

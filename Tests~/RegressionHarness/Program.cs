using UContent;
using UContent.Diagnostics;
using UContent.Internal;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;

class Program
{
    private static int passed;
    static void Check(bool condition, string message)
    { if (!condition) throw new Exception(message); }
    static Operation Done(object value)
    { var op = new Operation(); op.Succeed(value); return op; }
    static async Task Fails<T>(Func<Task> action) where T : Exception
    {
        try { await action().WaitAsync(TimeSpan.FromSeconds(3)); }
        catch (T) { return; }
        throw new Exception($"Expected {typeof(T).Name}");
    }
    static async Task Run(string name, Func<Task> action)
    {
        Check(ContentDiagnostics.Count == 0, "Leaked diagnostics before test");
        await action().WaitAsync(TimeSpan.FromSeconds(3));
        Check(ContentDiagnostics.Count == 0, "Leaked diagnostics after test");
        Check(Adapter.Unhandled.Count == 0, "Unhandled fire-and-forget error");
        Console.WriteLine("PASS " + name); passed++;
    }
    static async Task Main()
    {
        await Run("already completed asset stays retained until its handle is disposed", async () =>
        {
            var op = Done(new GameObject()); Addressables.Load = (_, _) => op;
            var handle = await new AddressablesContentService().LoadAsync<GameObject>("ready");
            Check(op.Releases == 0 && handle.Value != null, "Premature release");
            Check(ContentDiagnostics.Count == 1, "Missing handle diagnostic");
            handle.Dispose(); handle.Dispose();
            Check(op.Releases == 1, "Double release");
            await Fails<ObjectDisposedException>(() => Task.Run(() => _ = handle.Value));
        });
        await Run("concurrent loads share one operation and release after the last owner", async () =>
        {
            var op = new Operation(); Addressables.Load = (_, _) => op;
            var service = new AddressablesContentService(); var before = Addressables.LoadCalls;
            var a = service.LoadAsync<GameObject>("shared"); var b = service.LoadAsync<GameObject>("shared");
            Check(Addressables.LoadCalls == before + 1, "Not shared");
            op.Succeed(new GameObject()); var first = await a; var second = await b;
            first.Dispose(); Check(op.Releases == 0, "Other owner lost resource");
            second.Dispose(); Check(op.Releases == 1, "Missing release");
        });
        await Run("canceling one owner preserves another owner", async () =>
        {
            var op = new Operation(); Addressables.Load = (_, _) => op;
            var service = new AddressablesContentService(); using var cts = new CancellationTokenSource();
            var a = service.LoadAsync<GameObject>("cancel", cts.Token); var b = service.LoadAsync<GameObject>("cancel");
            cts.Cancel(); await Fails<OperationCanceledException>(async () => await a);
            Check(op.Releases == 0, "Canceled owner's load released too early");
            op.Succeed(new GameObject()); (await b).Dispose(); Check(op.Releases == 1, "Final owner leaked");
        });
        await Run("all canceled owners release the eventual result", async () =>
        {
            var op = new Operation(); Addressables.Load = (_, _) => op;
            using var cts = new CancellationTokenSource();
            var a = new AddressablesContentService().LoadAsync<GameObject>("cancel-all", cts.Token);
            cts.Cancel(); await Fails<OperationCanceledException>(async () => await a);
            op.Succeed(new GameObject()); Check(op.Releases == 1, "Orphaned result leaked");
        });
        await Run("an already canceled request starts no load or download", async () =>
        {
            using var cts = new CancellationTokenSource(); cts.Cancel();
            var service = new AddressablesContentService(); var loads = Addressables.LoadCalls; var downloads = Addressables.DownloadCalls;
            await Fails<OperationCanceledException>(async () => await service.LoadAsync<GameObject>("x", cts.Token));
            await Fails<OperationCanceledException>(async () => await service.DownloadAsync("x", cancellationToken: cts.Token));
            Check(Addressables.LoadCalls == loads && Addressables.DownloadCalls == downloads, "Canceled request started work");
        });
        await Run("synchronous load startup errors complete callers and permit retry", async () =>
        {
            Addressables.Load = (_, _) => throw new InvalidOperationException("start");
            var service = new AddressablesContentService();
            await Fails<ContentOperationException>(async () => await service.LoadAsync<GameObject>("retry"));
            var op = Done(new GameObject()); Addressables.Load = (_, _) => op;
            (await service.LoadAsync<GameObject>("retry")).Dispose();
            Check(op.Releases == 1, "Retry did not release");
        });
        await Run("failed shared asset loads free their operation and permit retry", async () =>
        {
            var op = new Operation(); Addressables.Load = (_, _) => op;
            var service = new AddressablesContentService(); var a = service.LoadAsync<GameObject>("bad"); var b = service.LoadAsync<GameObject>("bad");
            op.Fail(new Exception("failed"));
            await Fails<ContentOperationException>(async () => await a); await Fails<ContentOperationException>(async () => await b);
            Check(op.Releases == 1, "Failed load leaked");
            Addressables.Load = (_, _) => Done(new GameObject()); (await service.LoadAsync<GameObject>("bad")).Dispose();
        });
        await Run("disposing a scope during loading disposes the eventual handle", async () =>
        {
            var op = new Operation(); Addressables.Load = (_, _) => op;
            var scope = new AddressablesContentService().CreateScope("pending"); var pending = scope.LoadAsync<GameObject>("x");
            scope.Dispose(); op.Succeed(new GameObject());
            await Fails<ObjectDisposedException>(async () => await pending); Check(op.Releases == 1, "Scope leaked pending resource");
        });
        await Run("clear releases scope resources while retaining the reusable scope", async () =>
        {
            Addressables.Load = (_, _) => Done(new GameObject());
            var scope = new AddressablesContentService().CreateScope("level");
            await scope.LoadAsync<GameObject>("a"); await scope.LoadAsync<GameObject>("b");
            Check(scope.Count == 2 && ContentDiagnostics.Count == 3, "Wrong ownership diagnostics");
            scope.Clear(); Check(ContentDiagnostics.Count == 1 && scope.Count == 0, "Clear did not release handles");
            await scope.LoadAsync<GameObject>("c"); scope.Dispose(); scope.Dispose();
        });
        await Run("diagnostics observers cannot break resource ownership", async () =>
        {
            Action broken = () => throw new Exception("observer"); ContentDiagnostics.Changed += broken;
            try
            {
                var op = Done(new GameObject()); Addressables.Load = (_, _) => op;
                (await new AddressablesContentService().LoadAsync<GameObject>("diagnostic")).Dispose();
                Check(op.Releases == 1, "Observer broke disposal");
            }
            finally { ContentDiagnostics.Changed -= broken; }
        });
        await Run("instant downloads report final progress and isolate broken observers", async () =>
        {
            var op = Done(null); Addressables.Download = _ => op;
            var reports = 0; var progress = new Listener(_ => { reports++; throw new Exception("progress"); });
            await new AddressablesContentService().DownloadAsync("cached", progress);
            Check(reports == 1 && op.Releases == 1, "Missing final report or release");
        });
        await Run("progress mutation preserves the current snapshot of listeners", async () =>
        {
            var op = Done(null); var operation = new SharedDownloadOperation("x", () => new() { Operation = op }, null);
            var calls = 0; Listener first = null;
            first = new Listener(_ => operation.RemoveProgress(first)); var second = new Listener(_ => calls++);
            operation.AddProgress(first); operation.AddProgress(second); operation.Start(); await operation.Task;
            Check(calls == 1, "Listener removal skipped another observer");
        });
        await Run("the same observer can belong to two independent download requests", async () =>
        {
            var op = Done(null); var operation = new SharedDownloadOperation("x", () => new() { Operation = op }, null);
            var calls = 0; var progress = new Listener(_ => calls++);
            operation.AddProgress(progress); operation.AddProgress(progress); operation.RemoveProgress(progress);
            operation.Start(); await operation.Task; Check(calls == 1, "Canceling one registration removed another");
        });
        await Run("synchronous download startup failures complete callers and permit retry", async () =>
        {
            Addressables.Download = _ => throw new Exception("start"); var service = new AddressablesContentService();
            await Fails<ContentOperationException>(async () => await service.DownloadAsync("retry"));
            var op = Done(null); Addressables.Download = _ => op; await service.DownloadAsync("retry"); Check(op.Releases == 1, "Retry leaked");
        });
        await Run("shared download survives one canceled client and reports to the other", async () =>
        {
            var op = new Operation(); Addressables.Download = _ => op;
            var service = new AddressablesContentService(); var before = Addressables.DownloadCalls;
            using var cts = new CancellationTokenSource(); var reports = 0;
            var first = service.DownloadAsync("shared-download", cancellationToken: cts.Token);
            var second = service.DownloadAsync("shared-download", new Listener(_ => reports++));
            Check(Addressables.DownloadCalls == before + 1, "Download not shared");
            cts.Cancel(); await Fails<OperationCanceledException>(async () => await first);
            Check(op.Releases == 0, "Canceled waiter released shared download");
            op.Succeed(null); TestLoop.Tick(); await second;
            Check(op.Releases == 1 && reports >= 1, "Other client lost download or progress");
        });
        await Run("abandoned download finishes cleanup and a later request starts fresh", async () =>
        {
            var op = new Operation(); Addressables.Download = _ => op;
            var service = new AddressablesContentService(); using var cts = new CancellationTokenSource();
            var pending = service.DownloadAsync("abandoned", cancellationToken: cts.Token);
            cts.Cancel(); await Fails<OperationCanceledException>(async () => await pending);
            op.Succeed(null); TestLoop.Tick(); Check(op.Releases == 1, "Abandoned download leaked");
            var next = Done(null); Addressables.Download = _ => next;
            await service.DownloadAsync("abandoned"); Check(next.Releases == 1, "Completed entry remained cached");
        });
        await Run("inactive scene activation is exposed and concurrent unloads wait together", async () =>
        {
            var activation = new UniTaskCompletionSource(); var unloading = new Operation(); var activateCalls = 0; var unloadCalls = 0;
            Addressables.LoadScene = (_, _) => Done(new SceneInstance { Scene = new Scene(), Activate = () => { activateCalls++; return new AsyncOperation { Task = activation.Task }; } });
            Addressables.UnloadScene = () => { unloadCalls++; return unloading; };
            var scene = await new AddressablesContentService().LoadSceneAsync("level", activateOnLoad: false);
            var a = scene.ActivateAsync(); var b = scene.ActivateAsync(); var u1 = scene.UnloadAsync(); var u2 = scene.UnloadAsync();
            Check(activateCalls == 1 && unloadCalls == 0 && !scene.IsUnloaded, "Duplicate activation or premature unload");
            activation.TrySetResult(); await a; await b;
            Check(scene.IsActivated && unloadCalls == 1 && !u2.Inner.IsCompleted, "Unload did not share completion");
            unloading.Succeed(default(SceneInstance)); await u1; await u2;
            Check(scene.IsUnloaded && unloading.Releases == 1, "Unload did not complete"); await scene.UnloadAsync();
            await Fails<ObjectDisposedException>(() => Task.Run(() => scene.ActivateAsync()));
        });
        await Run("unloading an inactive scene activates it first", async () =>
        {
            var order = new List<string>();
            var scene = new ContentScene("inactive", new Scene(), () => { order.Add("activate"); return UniTask.CompletedTask; }, () => { order.Add("unload"); return UniTask.CompletedTask; }, false);
            await scene.UnloadAsync(); Check(string.Join(",", order) == "activate,unload", "Queue was not unblocked first");
        });
        await Run("activation and unload failures permit retry and preserve diagnostics", async () =>
        {
            var ac = 0; var uc = 0;
            var scene = new ContentScene("retry-scene", new Scene(), () => { if (++ac == 1) throw new Exception("activation"); return UniTask.CompletedTask; }, () => { if (++uc == 1) throw new Exception("unload"); return UniTask.CompletedTask; }, false);
            await Fails<Exception>(async () => await scene.UnloadAsync());
            Check(!scene.IsActivated && !scene.IsUnloaded && ContentDiagnostics.Count == 1, "Activation failure lost ownership");
            await Fails<Exception>(async () => await scene.UnloadAsync());
            Check(scene.IsActivated && !scene.IsUnloaded && ContentDiagnostics.Count == 1, "Unload failure lost ownership");
            await scene.UnloadAsync(); Check(ac == 2 && uc == 2, "Retry duplicated activation");
        });
        await Run("instance disposal is tracked and idempotent", async () =>
        {
            var op = Done(new GameObject()); Addressables.Instantiate = _ => op;
            var instance = await new AddressablesContentService().InstantiateAsync("prefab");
            Check(ContentDiagnostics.Count == 1, "Missing instance diagnostic"); instance.Dispose(); instance.Dispose(); Check(op.Releases == 1, "Double instance release");
        });
        Console.WriteLine($"{passed} regression scenarios passed.");
    }
    private sealed class Listener(Action<ContentDownloadProgress> action) : IProgress<ContentDownloadProgress>
    { public void Report(ContentDownloadProgress value) => action(value); }
}

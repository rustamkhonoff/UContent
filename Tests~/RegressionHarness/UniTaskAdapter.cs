// Test-only Task-backed adapter. Production compilation uses the real UniTask assemblies.
using System.Runtime.CompilerServices;
namespace Cysharp.Threading.Tasks
{
    [AsyncMethodBuilder(typeof(UniTaskBuilder))]
    public readonly struct UniTask
    {
        internal readonly Task Inner;
        public UniTask(Task task) => Inner = task;
        public TaskAwaiter GetAwaiter() => (Inner ?? Task.CompletedTask).GetAwaiter();
        public static UniTask CompletedTask => new(Task.CompletedTask);
        public static UniTask Yield() => TestLoop.NextFrame();
    }
    [AsyncMethodBuilder(typeof(UniTaskBuilder<>))]
    public readonly struct UniTask<T>
    {
        internal readonly Task<T> Inner;
        public UniTask(Task<T> task) => Inner = task;
        public TaskAwaiter<T> GetAwaiter() => Inner.GetAwaiter();
    }
    [AsyncMethodBuilder(typeof(UniTaskVoidBuilder))]
    public readonly struct UniTaskVoid
    {
        internal readonly Task Inner;
        public UniTaskVoid(Task task) => Inner = task;
        public void Forget() => Adapter.Track(Inner);
    }
    public static class TestLoop
    {
        private static readonly Queue<UniTaskCompletionSource> pending = new();
        public static UniTask NextFrame()
        {
            var source = new UniTaskCompletionSource(); pending.Enqueue(source); return source.Task;
        }
        public static void Tick()
        {
            var count = pending.Count;
            for (var i = 0; i < count; i++) pending.Dequeue().TrySetResult();
        }
    }
    public static class Adapter
    {
        public static readonly List<Exception> Unhandled = new();
        public static async void Track(Task task)
        {
            try { await task; }
            catch (Exception ex) { Unhandled.Add(ex); }
        }
        public static void Forget(this UniTask task) => Track(task.Inner ?? Task.CompletedTask);
        public static UniTask<T> AttachExternalCancellation<T>(this UniTask<T> task, CancellationToken token)
            => new(task.Inner.WaitAsync(token));
    }
    public sealed class UniTaskCompletionSource
    {
        private readonly TaskCompletionSource<bool> source = new();
        public UniTask Task => new(source.Task);
        public bool TrySetResult() => source.TrySetResult(true);
        public bool TrySetException(Exception ex) => source.TrySetException(ex);
    }
    public sealed class UniTaskCompletionSource<T>
    {
        private readonly TaskCompletionSource<T> source = new();
        public UniTask<T> Task => new(source.Task);
        public bool TrySetResult(T value) => source.TrySetResult(value);
        public bool TrySetException(Exception ex) => source.TrySetException(ex);
    }
    public struct UniTaskBuilder
    {
        private AsyncTaskMethodBuilder builder;
        public static UniTaskBuilder Create() => new() { builder = AsyncTaskMethodBuilder.Create() };
        public UniTask Task => new(builder.Task);
        public void SetResult() => builder.SetResult();
        public void SetException(Exception ex) => builder.SetException(ex);
        public void SetStateMachine(IAsyncStateMachine s) => builder.SetStateMachine(s);
        public void Start<T>(ref T s) where T : IAsyncStateMachine => builder.Start(ref s);
        public void AwaitOnCompleted<TA, TS>(ref TA a, ref TS s) where TA : INotifyCompletion where TS : IAsyncStateMachine => builder.AwaitOnCompleted(ref a, ref s);
        public void AwaitUnsafeOnCompleted<TA, TS>(ref TA a, ref TS s) where TA : ICriticalNotifyCompletion where TS : IAsyncStateMachine => builder.AwaitUnsafeOnCompleted(ref a, ref s);
    }
    public struct UniTaskBuilder<T>
    {
        private AsyncTaskMethodBuilder<T> builder;
        public static UniTaskBuilder<T> Create() => new() { builder = AsyncTaskMethodBuilder<T>.Create() };
        public UniTask<T> Task => new(builder.Task);
        public void SetResult(T value) => builder.SetResult(value);
        public void SetException(Exception ex) => builder.SetException(ex);
        public void SetStateMachine(IAsyncStateMachine s) => builder.SetStateMachine(s);
        public void Start<TS>(ref TS s) where TS : IAsyncStateMachine => builder.Start(ref s);
        public void AwaitOnCompleted<TA, TS>(ref TA a, ref TS s) where TA : INotifyCompletion where TS : IAsyncStateMachine => builder.AwaitOnCompleted(ref a, ref s);
        public void AwaitUnsafeOnCompleted<TA, TS>(ref TA a, ref TS s) where TA : ICriticalNotifyCompletion where TS : IAsyncStateMachine => builder.AwaitUnsafeOnCompleted(ref a, ref s);
    }
    public struct UniTaskVoidBuilder
    {
        private AsyncTaskMethodBuilder builder;
        public static UniTaskVoidBuilder Create() => new() { builder = AsyncTaskMethodBuilder.Create() };
        public UniTaskVoid Task => new(builder.Task);
        public void SetResult() => builder.SetResult();
        public void SetException(Exception ex) => builder.SetException(ex);
        public void SetStateMachine(IAsyncStateMachine s) => builder.SetStateMachine(s);
        public void Start<T>(ref T s) where T : IAsyncStateMachine => builder.Start(ref s);
        public void AwaitOnCompleted<TA, TS>(ref TA a, ref TS s) where TA : INotifyCompletion where TS : IAsyncStateMachine => builder.AwaitOnCompleted(ref a, ref s);
        public void AwaitUnsafeOnCompleted<TA, TS>(ref TA a, ref TS s) where TA : ICriticalNotifyCompletion where TS : IAsyncStateMachine => builder.AwaitUnsafeOnCompleted(ref a, ref s);
    }
}

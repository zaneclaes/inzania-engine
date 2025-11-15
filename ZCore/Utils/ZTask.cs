using System;
using System.Threading;
using System.Runtime.CompilerServices;
using IZ.Core.Utils;

#if Z_UNITY
using Cysharp.Threading.Tasks.CompilerServices;
using Cysharp.Threading.Tasks;
#else
using System.Threading.Tasks;
#endif

namespace IZ.Core.Utils
{
    // ============================================
    //  Non-generic ZTask
    // ============================================

    [AsyncMethodBuilder(typeof(ZTaskMethodBuilder))]
    public readonly struct ZTask
    {
#if Z_UNITY
        internal readonly UniTask _inner;

        public ZTask(UniTask inner) => _inner = inner;

        public UniTask.Awaiter GetAwaiter() => _inner.GetAwaiter();

        public static implicit operator ZTask(UniTask task) => new ZTask(task);
        public static implicit operator UniTask(ZTask ztask) => ztask._inner;
#else
        internal readonly Task _inner;

        public ZTask(Task inner) => _inner = inner ?? Task.CompletedTask;

        public TaskAwaiter GetAwaiter() => _inner.GetAwaiter();

        public static implicit operator ZTask(Task task) => new ZTask(task);
        public static implicit operator Task(ZTask ztask) => ztask._inner;
#endif

        // ---- common helpers ----

        public void Forget()
        {
#if Z_UNITY
            _inner.Forget();
#else
            // Fire-and-forget: just ignore the task. Optionally hook your own extension here.
            _ = _inner;
#endif
        }

        public static ZTask FromResult()
        {
#if Z_UNITY
            return new ZTask(UniTask.CompletedTask);
#else
            return new ZTask(Task.CompletedTask);
#endif
        }

        public static ZTask Delay(TimeSpan delay, CancellationToken token = default)
        {
#if Z_UNITY
            return new ZTask(UniTask.Delay(delay, cancellationToken: token));
#else
            return new ZTask(Task.Delay(delay, token));
#endif
        }

        public static ZTask Delay(int ms, CancellationToken token = default)
            => Delay(TimeSpan.FromMilliseconds(ms), token);

        public static ZTask WhenAll(params ZTask[] tasks)
        {
#if Z_UNITY
            var inner = new UniTask[tasks.Length];
            for (int i = 0; i < tasks.Length; i++)
                inner[i] = tasks[i]._inner;
            return new ZTask(UniTask.WhenAll(inner));
#else
            var inner = new Task[tasks.Length];
            for (int i = 0; i < tasks.Length; i++)
                inner[i] = tasks[i]._inner;
            return new ZTask(Task.WhenAll(inner));
#endif
        }

        public static ZTask CompletedTask
        {
            get
            {
#if Z_UNITY
                return new ZTask(UniTask.CompletedTask);
#else
                return new ZTask(Task.CompletedTask);
#endif
            }
        }

        public static ZTask FromException(Exception ex)
        {
#if Z_UNITY
            return new ZTask(UniTask.FromException(ex));
#else
            var tcs = new TaskCompletionSource<object?>();
            tcs.SetException(ex);
            return new ZTask(tcs.Task);
#endif
        }

        public static ZTask FromCanceled(CancellationToken token)
        {
#if Z_UNITY
            return new ZTask(UniTask.FromCanceled(token));
#else
            return new ZTask(Task.FromCanceled(token));
#endif
        }

        public static ZTask WaitUntil(Func<bool> predicate)
        {
#if Z_UNITY
            return new ZTask(UniTask.WaitUntil(predicate));
#else
            // Non-blocking loop: yields back to the scheduler each iteration.
            return new ZTask(Task.Run(async () =>
            {
                while (!predicate())
                    await Task.Yield();
            }));
#endif
        }

        public static ZTask WaitWhile(Func<bool> predicate)
        {
#if Z_UNITY
            return new ZTask(UniTask.WaitWhile(predicate));
#else
            return new ZTask(Task.Run(async () =>
            {
                while (predicate())
                    await Task.Yield();
            }));
#endif
        }

        public static ZTask Yield()
        {
#if Z_UNITY
            return new ZTask(UniTask.NextFrame());
#else
            // Tiny async delay to yield; does not block the thread.
            return new ZTask(Task.Delay(1));
#endif
        }

#if Z_UNITY
        public UniTask ToUniTask() => _inner;
#endif
    }

    // ============================================
    //  Generic ZTask<T>
    // ============================================

    [AsyncMethodBuilder(typeof(ZTaskMethodBuilder<>))]
    public readonly struct ZTask<T>
    {
#if Z_UNITY
        internal readonly UniTask<T> _inner;

        public ZTask(UniTask<T> inner) => _inner = inner;

        public UniTask<T>.Awaiter GetAwaiter() => _inner.GetAwaiter();

        public static implicit operator ZTask<T>(UniTask<T> task) => new ZTask<T>(task);
        public static implicit operator UniTask<T>(ZTask<T> ztask) => ztask._inner;
#else
        internal readonly Task<T> _inner;

        public ZTask(Task<T>? inner) => _inner = inner ?? Task.FromResult<T>(default!);

        public TaskAwaiter<T> GetAwaiter() => _inner.GetAwaiter();

        public static implicit operator ZTask<T>(Task<T> task) => new ZTask<T>(task);
        public static implicit operator Task<T>(ZTask<T> ztask) => ztask._inner;

        // NOTE: Result is potentially blocking; we guard it in browser.
        public T? Result
        {
            get
            {
                if (OperatingSystem.IsBrowser())
                    throw new InvalidOperationException(
                        "ZTask<T>.Result cannot be used in Blazor WebAssembly. Use 'await' instead.");
                return _inner.Result;
            }
        }
#endif

        // ---- helpers ----

        public T GetResult()
        {
#if Z_UNITY
            // UniTask<T>.GetResult() does not block the main Unity loop.
            return _inner.GetAwaiter().GetResult();
#else
            // On Blazor WASM, blocking on async will deadlock the single thread.
            if (OperatingSystem.IsBrowser())
                throw new InvalidOperationException(
                    "ZTask<T>.GetResult() cannot be used in Blazor WebAssembly. " +
                    "Use 'await' instead of blocking on the task.");

            // Still allowed on server/desktop.
            return _inner.GetAwaiter().GetResult();
#endif
        }

#if Z_UNITY
        public UniTask<T> ToUniTask<TTask>() where TTask : T => _inner;
#endif

        public static ZTask<T> FromResult(T value)
        {
#if Z_UNITY
            return new ZTask<T>(UniTask.FromResult(value));
#else
            return new ZTask<T>(Task.FromResult(value));
#endif
        }

        public static ZTask<T[]> WhenAll(params ZTask<T>[] tasks)
        {
#if Z_UNITY
            var inner = new UniTask<T>[tasks.Length];
            for (int i = 0; i < tasks.Length; i++)
                inner[i] = tasks[i]._inner;
            return new ZTask<T[]>(UniTask.WhenAll(inner));
#else
            var inner = new Task<T>[tasks.Length];
            for (int i = 0; i < tasks.Length; i++)
                inner[i] = tasks[i]._inner;
            return new ZTask<T[]>(Task.WhenAll(inner));
#endif
        }

        public static ZTask<T> FromException(Exception ex)
        {
#if Z_UNITY
            return new ZTask<T>(UniTask.FromException<T>(ex));
#else
            var tcs = new TaskCompletionSource<T>();
            tcs.SetException(ex);
            return new ZTask<T>(tcs.Task);
#endif
        }

        public static ZTask<T> FromCanceled(CancellationToken token)
        {
#if Z_UNITY
            return new ZTask<T>(UniTask.FromCanceled<T>(token));
#else
            return new ZTask<T>(Task.FromCanceled<T>(token));
#endif
        }
    }

    // ============================================
    //  Async method builders
    // ============================================

    public struct ZTaskMethodBuilder
    {
#if Z_UNITY
        private AsyncUniTaskMethodBuilder _builder;
#else
        private AsyncTaskMethodBuilder _builder;
#endif

        public static ZTaskMethodBuilder Create()
        {
#if Z_UNITY
            return new ZTaskMethodBuilder { _builder = AsyncUniTaskMethodBuilder.Create() };
#else
            return new ZTaskMethodBuilder { _builder = AsyncTaskMethodBuilder.Create() };
#endif
        }

        public ZTask Task
        {
            get
            {
#if Z_UNITY
                return new ZTask(_builder.Task);
#else
                return new ZTask(_builder.Task);
#endif
            }
        }

        public void SetException(Exception exception) => _builder.SetException(exception);
        public void SetResult() => _builder.SetResult();
        public void SetStateMachine(IAsyncStateMachine stateMachine) => _builder.SetStateMachine(stateMachine);

        public void Start<TStateMachine>(ref TStateMachine stateMachine)
            where TStateMachine : IAsyncStateMachine
            => _builder.Start(ref stateMachine);

        public void AwaitOnCompleted<TAwaiter, TStateMachine>(
            ref TAwaiter awaiter, ref TStateMachine stateMachine)
            where TAwaiter : INotifyCompletion
            where TStateMachine : IAsyncStateMachine
            => _builder.AwaitOnCompleted(ref awaiter, ref stateMachine);

        public void AwaitUnsafeOnCompleted<TAwaiter, TStateMachine>(
            ref TAwaiter awaiter, ref TStateMachine stateMachine)
            where TAwaiter : ICriticalNotifyCompletion
            where TStateMachine : IAsyncStateMachine
            => _builder.AwaitUnsafeOnCompleted(ref awaiter, ref stateMachine);
    }

    public struct ZTaskMethodBuilder<T>
    {
#if Z_UNITY
        private AsyncUniTaskMethodBuilder<T> _builder;
#else
        private AsyncTaskMethodBuilder<T> _builder;
#endif

        public static ZTaskMethodBuilder<T> Create()
        {
#if Z_UNITY
            return new ZTaskMethodBuilder<T> { _builder = AsyncUniTaskMethodBuilder<T>.Create() };
#else
            return new ZTaskMethodBuilder<T> { _builder = AsyncTaskMethodBuilder<T>.Create() };
#endif
        }

        public ZTask<T> Task
        {
            get
            {
#if Z_UNITY
                return new ZTask<T>(_builder.Task);
#else
                return new ZTask<T>(_builder.Task);
#endif
            }
        }

        public void SetException(Exception exception) => _builder.SetException(exception);
        public void SetResult(T result) => _builder.SetResult(result);
        public void SetStateMachine(IAsyncStateMachine stateMachine) => _builder.SetStateMachine(stateMachine);

        public void Start<TStateMachine>(ref TStateMachine stateMachine)
            where TStateMachine : IAsyncStateMachine
            => _builder.Start(ref stateMachine);

        public void AwaitOnCompleted<TAwaiter, TStateMachine>(
            ref TAwaiter awaiter, ref TStateMachine stateMachine)
            where TAwaiter : INotifyCompletion
            where TStateMachine : IAsyncStateMachine
            => _builder.AwaitOnCompleted(ref awaiter, ref stateMachine);

        public void AwaitUnsafeOnCompleted<TAwaiter, TStateMachine>(
            ref TAwaiter awaiter, ref TStateMachine stateMachine)
            where TAwaiter : ICriticalNotifyCompletion
            where TStateMachine : IAsyncStateMachine
            => _builder.AwaitUnsafeOnCompleted(ref awaiter, ref stateMachine);
    }

    public static class ZTaskExtensions
    {
        public static ZTask AsVoid<T>(this ZTask<T> source)
        {
#if Z_UNITY
            // Discard the TResult of UniTask<T>
            async UniTask Awaiter()
            {
                await source;   // await underlying UniTask<T>
            }

            return new ZTask(Awaiter());
#else
            async Task Awaiter()
            {
                await source;   // await underlying Task<T>
            }

            return new ZTask(Awaiter());
#endif
        }
    }
}

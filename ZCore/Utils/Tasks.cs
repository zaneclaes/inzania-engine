#region

#if ENABLE_UNITYWEBREQUEST
using Cysharp.Threading.Tasks;
#endif
using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
#if !Z_UNITY
using System.Threading.Tasks.Dataflow;
#endif
using IZ.Core.Contexts;
using IZ.Core.Observability.Logging;
using Microsoft.Extensions.DependencyInjection;

#endregion

namespace IZ.Core.Utils;

public static class Tasks {
  public static async Task WaitUntilAsync(Func<bool> condition, CancellationToken? ct = null, int pollDelay = 25) {
    try {
      while (!condition()) {
        await Task.Delay(pollDelay, ct ?? new CancellationToken()).ConfigureAwait(true);
      }
    } catch (TaskCanceledException) {
      // ignore: Task.Delay throws this exception when ct.IsCancellationRequested = true
      // In this case, we only want to stop polling and finish this async Task.
    }
  }

  public static async Task<object?> ExecuteObjectAsync(this Task task) {
    await task;
    var voidTaskType = typeof(Task<>).MakeGenericType(Type.GetType("System.Threading.Tasks.VoidTaskResult")!);
    if (voidTaskType.IsInstanceOfType(task))
      throw new InvalidOperationException("Task does not have a return value (" + task.GetType() + ")");
    var property = task.GetType().GetProperty("Result", BindingFlags.Public | BindingFlags.Instance);
    if (property == null)
      throw new InvalidOperationException("Task does not have a return value (" + task.GetType() + ")");
    return property.GetValue(task)!;

    // var resultProperty = typeof(Task<>).MakeGenericType(typeof(object)).GetProperty("Result");
    // return resultProperty!.GetValue(task);
  }

#if ENABLE_UNITYWEBREQUEST
    public static void Forget(this Task? task) {
      if (task != null) task.AsUniTask().Forget();
    }
#endif
#if !Z_UNITY
  public static void Forget(this Task? task) { }

  /// <summary>
  ///     Blocks until condition is true or task is canceled.
  /// </summary>
  /// <param name="ct">
  ///     Cancellation token.
  /// </param>
  /// <param name="condition">
  ///     The condition that will perpetuate the block.
  /// </param>
  /// <param name="pollDelay">
  ///     The delay at which the condition will be polled, in milliseconds.
  /// </param>
  /// <returns>
  ///     <see cref="Task" />.
  /// </returns>

  // https://stackoverflow.com/questions/13695499/proper-way-to-implement-a-never-ending-task-timers-vs-task
  public static ITargetBlock<DateTime> Create(
    Func<TimeSpan, CancellationToken, Task> action, TimeSpan interval, CancellationToken cancellationToken
  ) {
    // Declare the block variable, it needs to be captured.
    ActionBlock<DateTime>? block = null;
    var last = ZEnv.Now;

    block = new ActionBlock<DateTime>(async now => {
      var elapsed = ZEnv.Now - last;
      last = ZEnv.Now;
      await action(elapsed, cancellationToken);
      var remainingTime = interval - (ZEnv.Now - last);
      if (remainingTime > TimeSpan.Zero) await Task.Delay(remainingTime, cancellationToken).ConfigureAwait(false);
      // ReSharper disable once AccessToModifiedClosure
      block?.Post(ZEnv.Now);
    }, new ExecutionDataflowBlockOptions {
      CancellationToken = cancellationToken
    });

    return block;
  }

  public static WorkContext ScopeWork(this IServiceScope scope) {
    return new WorkContext(scope.ServiceProvider.GetRequiredService<ZApp>(), scope.ServiceProvider);
  }

  public static CancellationTokenSource ForeverLoop<TTask>(
    this IServiceScopeFactory factory, TimeSpan interval
  ) where TTask : ContextualObject, IForeverTask, new() {
    // Create the token source.
    var wtoken = new CancellationTokenSource();

    // Set the task.
    bool active = false;
    ITargetBlock<DateTime> task = Create(async (dt, token) => {
        if (active) return; // skip loop while active
        active = true;
        using var scope = factory.CreateScope();
        using var context = scope.ScopeWork();
        try {
          context.CancellationToken = wtoken.Token;
          // context.Log.Information("TASK start");
          await new TTask {
            Context = context
          }.RunTask(dt);
        } catch (Exception e) {
          context.HandleException(e, "LOOP", typeof(TTask).Name);
        } finally {
          active = false;
        }
      },
      interval,
      wtoken.Token);

    // Start the task.  Post the time.
    task.Post(ZEnv.Now);

    return wtoken;
  }
#endif

  public static Exception HandleException(this IZContext scope, Exception ex, string tag, string reason, ZEventLevel lvl = ZEventLevel.Error) {
    string errorType = ex.GetType().Name;
    if (scope.Span != null) {
      scope.Span.SetTag("error_type", errorType);
      scope.Span.SetTag("error_message", ex.Message);
      scope.Span.SetTag("error_level", lvl.ToString());
      if (lvl >= ZEventLevel.Error) scope.Span.SetException(ex);
    }

    scope.Log.Write(lvl,
      ex, "[{tag}]: {reason} {type}: {@error}", tag, reason, errorType, ZError.Guard(ex));
    return ex;
  }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using IZ.Core.Contexts;

namespace IZ.Core.Utils;

public abstract class TaskPoolBase : LogicBase {
  protected int MaxThreads { get; }

  public const int DefaultMaxThreads = 50;

  protected abstract List<Task> BaseTasks { get; }

  protected List<Task> ActiveTasks => BaseTasks.Where(t => t is {IsCompleted: false, IsFaulted: false, IsCanceled: false}).ToList();

  protected List<Task> FinishedTasks => BaseTasks.Where(t => t.IsCompleted || t.IsFaulted || t.IsCanceled).ToList();

  public TaskPoolBase(IZContext context, int maxThreads) : base(context) {
    MaxThreads = maxThreads;
  }

  public async Task Finish() => await Task.WhenAll(BaseTasks);

  public override string ToString() => $"<Tasks {FinishedTasks.Count}/{BaseTasks.Count} />";
}

public class TaskPool : TaskPoolBase {
  protected override List<Task> BaseTasks => Tasks;

  private List<Task> Tasks { get; } = new List<Task>();

  public TaskPool(IZContext context, int maxThreads = DefaultMaxThreads) : base(context, maxThreads) { }

  public async Task AddTask(Func<Task> task) {
    while (ActiveTasks.Count >= MaxThreads) await Task.Delay(100);
    // Log.Information("[TASK] adding to {pool}", this);
    Tasks.Add(task());
  }

  public static async Task RunAll<T>(IZContext context, List<T> paramz, Func<T, Task> creator, int maxThreads = DefaultMaxThreads) {
    using var pool = new TaskPool(context, maxThreads);
    foreach (var p in paramz) await pool.AddTask(() => creator(p));
    await pool.Finish();
  }
}

public class TaskPool<T> : TaskPoolBase {
  protected override List<Task> BaseTasks => Tasks.Cast<Task>().ToList();

  private List<Task<T>> Tasks { get; } = new List<Task<T>>();

  public TaskPool(IZContext context, int maxThreads = DefaultMaxThreads) : base(context, maxThreads) { }

  public async Task AddTask(Func<Task<T>> task) {
    while (ActiveTasks.Count >= MaxThreads) await Task.Delay(100);
    Tasks.Add(task());
  }

  public async Task<List<T>> GetResults() {
    await Finish();
    return Tasks.Select(t => t.Result).ToList();
  }

  public static async Task<List<T>> RunAll<TIn>(IZContext context, List<TIn> paramz, Func<TIn, Task<T>> creator, int maxThreads = DefaultMaxThreads) {
    using var pool = new TaskPool<T>(context, maxThreads);
    foreach (var p in paramz) await pool.AddTask(() => creator(p));
    return await pool.GetResults();
  }
}

public static class TaskPoolExtensions {
  public static Task RunTasks<TIn>(this IZContext context, List<TIn> paramz, Func<TIn, Task> creator, int maxThreads = TaskPoolBase.DefaultMaxThreads) =>
    TaskPool.RunAll(context, paramz, creator, maxThreads);

  public static Task RunTasks<TIn, TOut>(this IZContext context, List<TIn> paramz, Func<TIn, Task<TOut>> creator, int maxThreads = TaskPoolBase.DefaultMaxThreads) =>
    TaskPool<TOut>.RunAll(context, paramz, creator, maxThreads);
}

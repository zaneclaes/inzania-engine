#region

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GreenDonut;
using HotChocolate.Fetching;
using IZ.Core.Contexts;
using IZ.Core.Data;
using IZ.Core.Utils;
using Microsoft.Extensions.DependencyInjection;

#endregion

namespace IZ.Schema.Loaders;

public interface IZDataLoader {
  public string Key { get; }

  public Task Resolve();

  public int PendingCount { get; }

  public bool IsResolved { get; }

  public bool IsResolving { get; }
}

public interface IZDataLoader<TKey, TValue> : IZDataLoader {
  public void SetCacheEntry(TKey key, TValue value);

  public Task<TValue?> LoadAsync(TKey key);

  public Task<TValue?[]> LoadAsync(TKey[] keys);
}

public abstract class ZDataLoader<TKey, TValue> : LogicBase, IZDataLoader<TKey, TValue> where TKey : notnull {
  public string Key { get; }

  private string _id = ModelId.GenerateId();

  public bool IsResolved { get; private set; }

  public bool IsResolving { get; private set; }

  public int PendingCount => _queued.Count;

  protected readonly ConcurrentDictionary<TKey, TValue?> Data = new ConcurrentDictionary<TKey, TValue?>();

  private readonly ConcurrentBag<TKey> _queued = new ConcurrentBag<TKey>();

  protected ZDataLoader(IZContext context, string key) : base(context) {
    Key = key;
  }

  public void SetCacheEntry(TKey key, TValue? value) => Data[key] = value;

  protected abstract Task<IReadOnlyDictionary<TKey, TValue?>> GetData(TKey[] keys);

  public async Task Resolve() {
    var keys = _queued.Distinct().ToArray();
    if (!keys.Any()) {
      IsResolved = true;
      return;
    }
    try {
      IsResolving = true;
      _queued.Clear();
      var data = await GetData(keys);
      foreach (var k in data.Keys) Data[k] = data[k];
    } finally {
      IsResolved = !_queued.Any();
      IsResolving = false;
    }
  }

  public async Task<TValue?> LoadAsync(TKey key) {
    if (Data.TryGetValue(key, out var value)) return value;
    if (!_queued.Contains(key)) {
      if (IsResolved || IsResolving) {
        Log.Warning("[QUEUE] returning to un-loaded state...");
        IsResolved = false;
      }
      // Log.Information("[QUEUE] {key} into {self}", key, this);
      _queued.Add(key);
    }
    await Tasks.WaitUntil(() => IsResolved);
    return Data.GetValueOrDefault(key);
  }

  public async Task<TValue?[]> LoadAsync(TKey[] keys) {
    var tasks = keys.Select(LoadAsync).ToList();
    await Task.WhenAll(tasks);
    return tasks.Select(t => t.Result).ToArray();
  }

  public override string ToString() => $"<{Key} {GetType().Name}<{typeof(TKey).Name}, {typeof(TValue).Name}>#{_id} />";
}

public class SingleDataLoader<TKey, TValue> : ZDataLoader<TKey, TValue> where TKey : notnull {

  private readonly FetchBatch<TKey, TValue> _fetch;

  public SingleDataLoader(IZContext context, string key, FetchBatch<TKey, TValue> fetch) : base(context, key) {
    _fetch = fetch ?? throw new ArgumentNullException(nameof(fetch));
  }

  protected override Task<IReadOnlyDictionary<TKey, TValue?>> GetData(TKey[] keys) => _fetch(keys, Context.CancellationToken)!;

  // protected override string CacheKeyType { get; }
  //
  // protected override Task<IReadOnlyDictionary<TKey, TValue>> LoadBatchAsync(
  //   IReadOnlyList<TKey> keys,
  //   CancellationToken cancellationToken) =>
  //   _fetch(keys, cancellationToken);

}

internal sealed class MultiDataLoader<TKey, TValue> : ZDataLoader<TKey, TValue[]> where TKey : notnull {
  private readonly FetchGroup<TKey, TValue> _fetch;

  public MultiDataLoader(IZContext context, string key, FetchGroup<TKey, TValue> fetch) : base(context, key) {
    _fetch = fetch ?? throw new ArgumentNullException(nameof(fetch));
  }

  // protected override Task<ILookup<TKey, TValue>> LoadGroupedBatchAsync(
  //   IReadOnlyList<TKey> keys,
  //   CancellationToken cancellationToken) =>
  //   _fetch(keys, cancellationToken);
  protected override async Task<IReadOnlyDictionary<TKey, TValue[]?>> GetData(TKey[] keys) {
    var res = await _fetch(keys, Context.CancellationToken);
    return res.ToDictionary(r => r.Key, r => r.ToArray())!;
  }
}

// public class SingleDataLoader<TKey, TValue>
//   : BatchDataLoader<TKey, TValue>, GreenDonut.IDataLoader<TKey, TValue>
//   where TKey : notnull {
//   private readonly FetchBatch<TKey, TValue> _fetch;
//
//   private string _id = ModelId.GenerateId();
//
//   public SingleDataLoader(
//     IZContext context,
//     string key,
//     FetchBatch<TKey, TValue> fetch)
//     : base(context.ServiceProvider.GetRequiredService<IBatchScheduler>(), context.ServiceProvider.GetRequiredService<DataLoaderOptions>()) {
//     _fetch = fetch ?? throw new ArgumentNullException(nameof(fetch));
//     CacheKeyType = $"{GetCacheKeyType(GetType())}-{key}";
//   }
//
//   protected override string CacheKeyType { get; }
//
//   protected override Task<IReadOnlyDictionary<TKey, TValue>> LoadBatchAsync(
//     IReadOnlyList<TKey> keys,
//     CancellationToken cancellationToken) =>
//     _fetch(keys, cancellationToken);
//
//   public override string ToString() => $"<{GetType().Name}<{typeof(TKey).Name}, {typeof(TValue).Name}>#{_id} />";
// }
//
//
// internal sealed class MultiDataLoader<TKey, TValue>
//   : GroupedDataLoader<TKey, TValue>, GreenDonut.IDataLoader<TKey, TValue[]>
//   where TKey : notnull {
//   private readonly FetchGroup<TKey, TValue> _fetch;
//
//   private string _id = ModelId.GenerateId();
//
//   public MultiDataLoader(
//     IZContext context,
//     string key,
//     FetchGroup<TKey, TValue> fetch)
//     : base(context.ServiceProvider.GetRequiredService<IBatchScheduler>(), context.ServiceProvider.GetRequiredService<DataLoaderOptions>()) {
//     _fetch = fetch ?? throw new ArgumentNullException(nameof(fetch));
//     CacheKeyType = $"{GetCacheKeyType(GetType())}-{key}";
//   }
//
//   protected override string CacheKeyType { get; }
//
//   protected override Task<ILookup<TKey, TValue>> LoadGroupedBatchAsync(
//     IReadOnlyList<TKey> keys,
//     CancellationToken cancellationToken) =>
//     _fetch(keys, cancellationToken);
//
//   public override string ToString() => $"<{GetType().Name}<{typeof(TKey).Name}, {typeof(TValue).Name}#{_id} />";
// }

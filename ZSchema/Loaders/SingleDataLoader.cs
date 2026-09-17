#region

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HotChocolate.Fetching;
using IZ.Core.Contexts;
using IZ.Core.Data;
using IZ.Core.Utils;

#endregion

namespace IZ.Schema.Loaders;

public interface IZDataLoader {
  public string Key { get; }

  public int PendingCount { get; }

  public bool IsResolved { get; }

  public bool IsResolving { get; }

  public Task Resolve();
}

public interface IZDataLoader<TKey, TValue> : IZDataLoader {
  public void SetCacheEntry(TKey key, TValue value);

  public Task<TValue?> LoadAsync(TKey key);

  public Task<TValue?[]> LoadAsync(TKey[] keys);
}

public abstract class ZDataLoader<TKey, TValue> : LogicBase, IZDataLoader<TKey, TValue> where TKey : notnull {

  /// <summary>Joining the queue and observing a cycle have to be one indivisible step. They were not,
  /// and <see cref="LoadAsync(TKey)" /> says what that cost.</summary>
  private readonly object _gate = new object();

  private readonly HashSet<TKey> _queued = new HashSet<TKey>();

  /// <summary>Cycles that have taken their keys, counted under <see cref="_gate" />. A waiter
  /// remembers the value it joined on, which is what tells a failure that answered its own batch from
  /// one that had already taken its keys before this caller ever asked.</summary>
  private int _cycles;

  /// <summary>The last cycle in which a batch holding this key threw. A waiter wakes on it and nothing
  /// else reads it: the failure is answered, never written into <see cref="Data" /> as a value, so a
  /// later load of the same key in the same request queues again. It holds one int per key a failed
  /// batch took, and is empty unless something threw — bounded by the request, because
  /// <c>ZSchema.AddSchemaServices</c> registers <c>ZDataLoaderRegistry</c> and the resolver scoped, so
  /// the loader and this map die with it.</summary>
  private readonly ConcurrentDictionary<TKey, int> _failedAt = new ConcurrentDictionary<TKey, int>();

  /// <summary>Every key this loader has answered — hit or miss, a miss recorded as <c>default</c>.
  /// Recording the miss is what makes "this key was asked about" observable, and that is the only
  /// thing a waiter can wait on that is about its own key rather than about the loader's mood.</summary>
  protected readonly ConcurrentDictionary<TKey, TValue?> Data = new ConcurrentDictionary<TKey, TValue?>();

  private readonly string _id = ModelId.GenerateId();

  protected ZDataLoader(IZContext context, string key) : base(context) {
    Key = key;
  }
  public string Key { get; }

  public bool IsResolved => !IsResolving && PendingCount == 0;

  public bool IsResolving { get; private set; }

  public int PendingCount {
    get {
      lock (_gate) return _queued.Count;
    }
  }

  public void SetCacheEntry(TKey key, TValue? value) => Data[key] = value;

  public async Task Resolve() {
    TKey[] keys;
    int cycle;
    lock (_gate) {
      // One cycle at a time. Keys queued while this one runs stay queued, and the resolver comes back
      // for them: `ZSchemaResolver.Resolve` recurses while any loader still has pending keys.
      if (IsResolving || _queued.Count == 0) return;
      keys = new TKey[_queued.Count];
      _queued.CopyTo(keys);
      _queued.Clear();
      cycle = _cycles++;
      IsResolving = true;
    }
    bool failed = false;
    try {
      IReadOnlyDictionary<TKey, TValue?> data = await GetData(keys);
      foreach (var k in data.Keys) Data[k] = data[k];
    } catch (Exception e) {
      // A failed batch still has to answer its keys. Swallowing here is deliberate: this runs inside
      // the resolver's fire-and-forget scheduling task, where an exception is unobserved and would
      // strand every other key in the request. The answer is the same null the old code gave — the
      // difference is that the reason is now in the log instead of nowhere.
      failed = true;
      Log.Error(e, "[RES] {self} failed to load {count} key(s)", this, keys.Length);
    } finally {
      if (failed) {
        // A failure is answered, not recorded. Writing `default` here would make a transient fault (a
        // deadlock, a timeout) indistinguishable from "the row is absent" for the rest of the request
        // — the same null-for-a-row-that-exists this loader was rewritten to stop — and would eat the
        // retry the pre-repair code had. Recording the CYCLE releases exactly this batch's waiters and
        // leaves the key absent, so a later `LoadAsync` queues it again. Nothing is re-queued here: a
        // structural failure would then spin `ZSchemaResolver.Resolve`'s pending-key recursion.
        // `Docs/Plans/data/2026-09-17-repair-12.md`.
        foreach (var k in keys) _failedAt[k] = cycle;
      } else {
        // A key that was asked about is answered, even when the source had nothing for it. Without
        // this, "absent" and "not asked yet" are the same state and a waiter on a genuinely missing
        // row never finishes — which is why the old code had to wait on a loader-wide flag instead.
        foreach (var k in keys) Data.TryAdd(k, default);
      }
      lock (_gate) IsResolving = false;
    }
  }

  public async Task<TValue?> LoadAsync(TKey key) {
    if (Data.TryGetValue(key, out var value)) return value;
    int joined;
    lock (_gate) {
      // Read the cycle count under the same lock a cycle takes its keys under. A cycle that had
      // already started cannot answer this caller, and any cycle that starts after this point holds
      // this key — so a failure can only release the waiters that were actually in its batch.
      joined = _cycles;
      if (!Data.ContainsKey(key)) _queued.Add(key);
    }
    // Wait for THIS key. This used to wait on a shared "the loader has nothing queued" flag, and
    // nothing held that flag and the queue together: `Resolve`'s old `IsResolved = !_queued.Any()`
    // read the queue and then wrote the flag, so a join landing in between was overwritten, and an
    // empty-queue cycle set it outright. The waiter then woke on someone else's cycle and read
    // nothing for a row that exists — on the wire, `video: null` on a page that has one, and, for a
    // reference C# declares non-nullable, `ArgumentNullException` out of the generated type map,
    // which failed the entire query. `Docs/Plans/data/2026-09-17-client-cache-repair.md`.
    // A batch that threw is the other way out: it answers its own waiters without leaving a value
    // behind. `TryGetValue`, not `GetValueOrDefault` — `joined` is 0 on the first cycle, and a 0
    // default would release every first waiter on the spot.
    await Tasks.WaitUntil(() => Data.ContainsKey(key) || (_failedAt.TryGetValue(key, out int at) && at >= joined));
    return Data.GetValueOrDefault(key);
  }

  public async Task<TValue?[]> LoadAsync(TKey[] keys) {
    List<Task<TValue?>> tasks = keys.Select(LoadAsync).ToList();
    await Task.WhenAll(tasks);
    return tasks.Select(t => t.Result).ToArray();
  }

  protected abstract Task<IReadOnlyDictionary<TKey, TValue?>> GetData(TKey[] keys);

  public override string ToString() => $"<{Key}#{_id} />";
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
    ILookup<TKey, TValue> res = await _fetch(keys, Context.CancellationToken);
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

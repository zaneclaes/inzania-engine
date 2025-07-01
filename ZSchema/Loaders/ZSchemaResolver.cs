#region

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
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

public class ZSchemaResolver : LogicBase, IZResolver {
  private List<IZDataLoader> PendingLoaders => _dataLoaders.Values.Where(l => !l.IsResolved && !l.IsResolving).ToList();

  // A poor man's approach to scheduled batching... bake in a delay after which the resolution will occur if no new tasks were queued.
  // As more resolutions are scheduled, the delay increases, so single items are fast but when giant batches happen they are given time to acrue
  private int ResolveDelayMs => Math.Clamp((int)Math.Pow(_resolutions, 1/2f), 1, 20);

  private long _resolveAt = 0;

  private int _resolutions = 0;

  private readonly Stopwatch _stopwatch = Stopwatch.StartNew();

  private async Task Resolve() {
    var loaders = PendingLoaders;
    if (!loaders.Any()) return;
    var cnt = loaders.Sum(l => l.PendingCount);
    Log.Debug("[RES] {self} resolving {cnt}", this, cnt);
    await Task.WhenAll(loaders.Select(l => l.Resolve()));
    _resolutions -= cnt;
    if (_resolutions < 0) _resolutions = 0;
    await Resolve();
  }

  private Task? _resolutionTask;

  private void ScheduleResolution(int cnt) {
    _resolutions += cnt;
    _resolveAt = _stopwatch.ElapsedMilliseconds + ResolveDelayMs;
    _resolutionTask ??= Task.Run(async () => {
      while (_stopwatch.ElapsedMilliseconds < _resolveAt) await Task.Delay(1);
      _resolutionTask = null;
      await Resolve();
    });
  }

  private readonly ConcurrentDictionary<string, IZDataLoader> _dataLoaders = new ConcurrentDictionary<string, IZDataLoader>();

  public ZSchemaResolver(IZContext context) : base(context) {
    // Log.Information("[RES] new resolver {res} for {context} : {stack}", this, context.Root, new ZTrace(new StackTrace().ToString()).ToString());
  }

  public async Task<TData[]> LoadArray<TKey, TData>(
    string name, Func<IReadOnlyList<TKey>, Task<ILookup<TKey, TData>>> load, TKey? key, List<TData> existing
  ) where TKey : notnull where TData : class {
    if (!name.EndsWith("[]")) name += "[]";
    if (key == null) return new TData[] { };
    try {
      IZDataLoader<TKey, TData[]>? loader = _dataLoaders.TryGetValue(name, out var dataLoader) ?
        dataLoader as IZDataLoader<TKey, TData[]> : null;
      if (loader == null) {
        _dataLoaders[name] = loader = Context.ServiceProvider.GetRequiredService<ZDataLoaderRegistry>().GroupDataLoader<TKey, TData>(Context, name, async (keys, token) => {
          // Log.Information("[RES] {name} begin {@keys} with {res} in {context}", name, keys, this, Context.Root);
          return await load(keys);
        });
      }
      // Log.Information("[RES] {name} queue {key} in {loader} with {res} in {context}", name, key, loader, this, Context.Root);

      if (existing.Any()) {
        TData[] ret = existing.ToArray();
        loader.SetCacheEntry(key, ret);
        return ret;
      }

      ScheduleResolution(1);
      return await loader.LoadAsync(key) ??  new TData[] { };
    } catch (Exception e) {
      if (!(e is TaskCanceledException)) Log.Warning(e, "[RES] failed to resolve array {name}", name);
      throw;
    }
  }

  public async Task<IReadOnlyList<TData>> LoadMany<TKey, TData>(
    string name, Func<IReadOnlyList<TKey>, Task<Dictionary<TKey, List<TData>>>> load, List<TKey> keys, List<TData> existing, Func<TData, TKey?> fetchKey
  ) where TKey : notnull where TData : class {
    try {
      if (keys.Any(k => k == null)) throw new NullReferenceException(nameof(keys));
      Log.Verbose("[RES2] {name} queue {key}", name, keys);
      // IScope outerScope = Tracer.Instance.ActiveScope;
      while (name.EndsWith("[]")) name = name.Substring(0, name.Length - 2);
      IZDataLoader<TKey, List<TData>>? loader = _dataLoaders.TryGetValue(name, out var dataLoader) ? dataLoader as IZDataLoader<TKey, List<TData>> : null;
      if (loader == null) {
        _dataLoaders[name] = loader = Context.ServiceProvider.GetRequiredService<ZDataLoaderRegistry>().SingleDataLoader<TKey, List<TData>>(Context, name, async (k, token) => (await load(k)).ToImmutableDictionary());
      }

      // foreach (var exist in existing) {
      //   if (exist != null) {
      //     var key = fetchKey(exist);
      //     if (key != null) loader.SetCacheEntry(key, exist);
      //   }

      // Log.Information("[LOAD ALL] {keys}", keys.ToList());
      ScheduleResolution(keys.Count);
      return (await loader.LoadAsync(keys.ToArray())).Where(v => v != null)
        .SelectMany(v => v!.ToList()).Where(v => v != null).ToImmutableList();
    } catch (Exception e) {
      if (!(e is TaskCanceledException)) Log.Warning(e, "[RES] failed to resolve list {name}", name);
      throw;
    }
    // TData[] ret = await LoadArray(name, load, key, existing == null ? new List<TData>() : new List<TData>() { existing });
    // return ret.FirstOrDefault();
  }


  public async Task<IReadOnlyList<TData>> LoadAll<TKey, TData>(
    string name, Func<IReadOnlyList<TKey>, Task<Dictionary<TKey, TData>>> load, List<TKey> keys, List<TData> existing, Func<TData, TKey?> fetchKey
  ) where TKey : notnull where TData : class {
    try {
      if (keys.Any(k => k == null)) throw new NullReferenceException(nameof(keys));
      Log.Verbose("[RES2] {name} queue {key}", name, keys);
      // IScope outerScope = Tracer.Instance.ActiveScope;
      while (name.EndsWith("[]")) name = name.Substring(0, name.Length - 2);
      IZDataLoader<TKey, TData>? loader = _dataLoaders.TryGetValue(name, out var dataLoader) ? dataLoader as IZDataLoader<TKey, TData> : null;
      if (loader == null) {
        _dataLoaders[name] = loader = Context.ServiceProvider.GetRequiredService<ZDataLoaderRegistry>().SingleDataLoader<TKey, TData>(Context, name, async (k, token) => (await load(k)).ToImmutableDictionary());
      }

      foreach (var exist in existing) {
        if (exist != null) {
          var key = fetchKey(exist);
          if (key != null) loader.SetCacheEntry(key, exist);
        }
      }

      // if (existing != null) {
      //   loader.Set(key, existing);
      //   return existing;
      // }

      // Log.Information("[LOAD ALL] {keys}", keys.ToList());
      ScheduleResolution(keys.Count);
      return (await loader.LoadAsync(keys.ToArray())).Where(v => v != null).Cast<TData>().ToImmutableList();
    } catch (Exception e) {
      if (!(e is TaskCanceledException)) Log.Warning(e, "[RES] failed to resolve all {name}", name);
      throw;
    }
    // TData[] ret = await LoadArray(name, load, key, existing == null ? new List<TData>() : new List<TData>() { existing });
    // return ret.FirstOrDefault();
  }


  // private IDataLoader<TKey, TData> SingleDataLoader<TKey, TData>(string key, FetchBatch<TKey, TData> fetch) where TKey : notnull where TData : class {
  //   if (key == null) throw new NullReferenceException(nameof(key));
  //   var services = Context.ServiceProvider!;
  //   var loader = Context.ServiceProvider.GetRequiredService<ZDataLoaderRegistry>().SingleDataLoader(Context, key, fetch);
  //
  //   // Warm the cache with existing models from EFCore's change tracker
  //   // var user = Context.CurrentIdentity?.IZUser;
  //   // if (user != null && user.GetType().IsAssignableTo(typeof(TData))) {
  //   //   Log.Verbose("[DATA] special user: {user}", user);
  //   //   loader.SetCacheEntry(user.Id, user);
  //   // }
  //   // if (typeof(TData).IsSubclassOf(typeof(ModelKey<TKey>))) {
  //   //   var mems = await Context.Data.GetMemoryModels<TData>();
  //   //   foreach (var model in mems) {
  //   //     Log.Verbose("[DATA] provide {type} {model}", typeof(TData), model);
  //   //     loader.SetCacheEntry((model as ModelKey<TKey>)!.Id, model);
  //   //   }
  //   // }
  //   return loader;
  // }
  //
  // private IDataLoader<TKey, TData[]> GroupDataLoader<TKey, TData>(string key, FetchGroup<TKey, TData> fetch) where TKey : notnull where TData : class {
  //   if (key == null) throw new NullReferenceException(nameof(key));
  //   var services = Context.ServiceProvider!;
  //   var loader = Context.ServiceProvider.GetRequiredService<ZDataLoaderRegistry>().GroupDataLoader(Context, key, fetch);
  //
  //   // Warm the cache with existing models from EFCore's change tracker
  //   // var user = Context.CurrentIdentity?.IZUser;
  //   // if (user != null && user.GetType().IsAssignableTo(typeof(TData))) {
  //   //   Log.Verbose("[DATA] special user: {user}", user);
  //   //   loader.SetCacheEntry(user.Id, user);
  //   // }
  //   // if (typeof(TData).IsSubclassOf(typeof(ModelKey<TKey>))) {
  //   //   var mems = await Context.Data.GetMemoryModels<TData>();
  //   //   foreach (var model in mems) {
  //   //     Log.Verbose("[DATA] provide {type} {model}", typeof(TData), model);
  //   //     loader.SetCacheEntry((model as ModelKey<TKey>)!.Id, model);
  //   //   }
  //   // }
  //   return loader;
  // }

  public override void Dispose() {
    base.Dispose();
    // Log.Information("[DISP] Resolver for {keys}", _dataLoaders.Keys.ToList());
    _dataLoaders.Clear();
  }
}

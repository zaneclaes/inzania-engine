#region

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using GreenDonut;
using HotChocolate.Fetching;
using IZ.Core.Contexts;
using IZ.Core.Data;
using IZ.Schema.Loaders;
using Microsoft.Extensions.DependencyInjection;

#endregion

namespace IZ.Schema;

public class DataLoaderRegistry {
  private readonly ConcurrentDictionary<string, object> _groupLoaders = new ConcurrentDictionary<string, object>();

  public IDataLoader<TKey, TValue[]> GroupDataLoader<TKey, TValue>(IServiceProvider sp, string key, FetchGroup<TKey, TValue> fetch) where TKey : notnull =>
    (_groupLoaders.GetOrAdd(key, (k) =>
      new MultiDataLoader<TKey, TValue>(k, fetch, sp)
    ) as IDataLoader<TKey, TValue[]>) ?? throw new ArgumentException($"Failed to create DataLoader {key}");

  private readonly ConcurrentDictionary<string, object> _singleLoaders = new ConcurrentDictionary<string, object>();

  public IDataLoader<TKey, TValue> SingleDataLoader<TKey, TValue>(IServiceProvider sp, string key, FetchBatch<TKey, TValue> fetch) where TKey : notnull =>
    (_singleLoaders.GetOrAdd(key, (k) =>
      new SingleDataLoader<TKey, TValue>(k, fetch, sp)
    ) as IDataLoader<TKey, TValue>) ?? throw new ArgumentException($"Failed to create DataLoader {key}");
}

public class TuneSchemaResolver : LogicBase, ITuneResolver {
  private readonly Dictionary<string, IDataLoader> _dataLoaders = new Dictionary<string, IDataLoader>();

  public TuneSchemaResolver(ITuneContext context) : base(context) {
    Log.Verbose("[RES] new resolver");
  }

  public async Task<TData[]> LoadArray<TKey, TData>(
    string name, Func<IReadOnlyList<TKey>, Task<ILookup<TKey, TData>>> load, TKey? key, List<TData> existing
  ) where TKey : notnull {
    Log.Verbose("[RES] {name} queue {key}", name, key);
    if (key == null) return new TData[] { };
    try {
      // IScope outerScope = Tracer.Instance.ActiveScope;
      // if (existing.Any()) return Task.FromResult(existing.ToArray());
      if (!name.EndsWith("[]")) name += "[]";
      IDataLoader<TKey, TData[]>? loader = _dataLoaders.TryGetValue(name, out var dataLoader) ?
        dataLoader as IDataLoader<TKey, TData[]> : null;
      if (loader == null) {
        _dataLoaders[name] = loader = GroupDataLoader<TKey, TData>(name, async (keys, token) => {
          // using var op = new FurSpan("DB", name);
          // using var op = Context
          Log.Verbose("[RES] {name} begin {@keys}", name, keys);
          // return await Context.Data.ExecuteLocked(() => load(keys));
          return await load(keys);
        });
      }

      if (existing.Any()) {
        TData[] ret = existing.ToArray();
        loader.Set(key, ret);
        return ret;
      }

      return (await loader.LoadAsync(key) as TData[])!;
    } catch (Exception e) {
      if (!(e is TaskCanceledException)) Log.Warning(e, "[RES] failed to resolve {name}", name);
      throw;
    }
  }

  public async Task<IReadOnlyList<TData>> LoadMany<TKey, TData>(
    string name, Func<IReadOnlyList<TKey>, Task<Dictionary<TKey, List<TData>>>> load, List<TKey> keys, List<TData> existing, Func<TData, TKey> fetchKey
  ) where TKey : notnull {
    try {
      if (keys.Any(k => k == null)) throw new NullReferenceException(nameof(keys));
      Log.Verbose("[RES2] {name} queue {key}", name, keys);
      // IScope outerScope = Tracer.Instance.ActiveScope;
      while (name.EndsWith("[]")) name = name.Substring(0, name.Length - 2);
      IDataLoader<TKey, List<TData>>? loader = _dataLoaders.TryGetValue(name, out var dataLoader) ? dataLoader as IDataLoader<TKey, List<TData>> : null;
      if (loader == null) {
        _dataLoaders[name] = loader = SingleDataLoader<TKey, List<TData>>(name, async (k, token) => {
          // using var op = new FurSpan("DB", name);
          // Log.Information("[LOAD ARR] {keys}", k.ToList());
          // return await Context.Data.ExecuteLocked(async () => (await load(k)).ToImmutableDictionary());
          return (await load(k)).ToImmutableDictionary();
          // await _semaphore.WaitAsync(token);
          // try {
          //   return (await load(k)).ToImmutableDictionary();
          // } finally {
          //   _semaphore.Release();
          // }
        });
      }

      foreach (var exist in existing) {
        if (exist != null) {
          var key = fetchKey(exist);
          if (key != null) loader.Set(key, exist);
        }
      }

      // if (existing != null) {
      //   loader.Set(key, existing);
      //   return existing;
      // }

      // Log.Information("[LOAD ALL] {keys}", keys.ToList());
      return (await loader.LoadAsync(keys.ToArray())).Where(v => v != null)
        .SelectMany(v => v.ToList()).Where(v => v != null).ToImmutableList();
    } catch (Exception e) {
      if (!(e is TaskCanceledException)) Log.Warning(e, "[RES] failed to resolve {name}", name);
      throw;
    }
    // TData[] ret = await LoadArray(name, load, key, existing == null ? new List<TData>() : new List<TData>() { existing });
    // return ret.FirstOrDefault();
  }


  public async Task<IReadOnlyList<TData>> LoadAll<TKey, TData>(
    string name, Func<IReadOnlyList<TKey>, Task<Dictionary<TKey, TData>>> load, List<TKey> keys, List<TData> existing, Func<TData, TKey> fetchKey
  ) where TKey : notnull {
    try {
      if (keys.Any(k => k == null)) throw new NullReferenceException(nameof(keys));
      Log.Verbose("[RES2] {name} queue {key}", name, keys);
      // IScope outerScope = Tracer.Instance.ActiveScope;
      while (name.EndsWith("[]")) name = name.Substring(0, name.Length - 2);
      IDataLoader<TKey, TData>? loader = _dataLoaders.TryGetValue(name, out var dataLoader) ? dataLoader as IDataLoader<TKey, TData> : null;
      if (loader == null) {
        _dataLoaders[name] = loader = SingleDataLoader<TKey, TData>(name, async (k, token) => {
          // using var op = new FurSpan("DB", name);
          // Log.Information("[LOAD ARR] {keys}", k.ToList());
          // return await Context.Data.ExecuteLocked(async () => (await load(k)).ToImmutableDictionary());
          return (await load(k)).ToImmutableDictionary();
          // await _semaphore.WaitAsync(token);
          // try {
          //   return (await load(k)).ToImmutableDictionary();
          // } finally {
          //   _semaphore.Release();
          // }
        });
      }

      foreach (var exist in existing) {
        if (exist != null) {
          var key = fetchKey(exist);
          if (key != null) loader.Set(key, exist);
        }
      }

      // if (existing != null) {
      //   loader.Set(key, existing);
      //   return existing;
      // }

      // Log.Information("[LOAD ALL] {keys}", keys.ToList());
      return (await loader.LoadAsync(keys.ToArray())).Where(v => v != null).ToImmutableList();
    } catch (Exception e) {
      if (!(e is TaskCanceledException)) Log.Warning(e, "[RES] failed to resolve {name}", name);
      throw;
    }
    // TData[] ret = await LoadArray(name, load, key, existing == null ? new List<TData>() : new List<TData>() { existing });
    // return ret.FirstOrDefault();
  }


  private IDataLoader<TKey, TValue> SingleDataLoader<TKey, TValue>(string key, FetchBatch<TKey, TValue> fetch) where TKey : notnull {
    if (key == null) throw new NullReferenceException(nameof(key));
    var services = Context.ServiceProvider!;
    return services.GetRequiredService<DataLoaderRegistry>().SingleDataLoader(services, key, fetch);
  }

  public IDataLoader<TKey, TValue[]> GroupDataLoader<TKey, TValue>(string key, FetchGroup<TKey, TValue> fetch) where TKey : notnull {
    if (key == null) throw new NullReferenceException(nameof(key));
    var services = Context.ServiceProvider!;
    return services.GetRequiredService<DataLoaderRegistry>().GroupDataLoader(services, key, fetch);
  }

  public override void Dispose() {
    base.Dispose();
    // Log.Information("[DISP] Resolver for {keys}", _dataLoaders.Keys.ToList());
    _dataLoaders.Clear();
  }
}

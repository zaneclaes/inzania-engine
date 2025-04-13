#region

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
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
  private readonly Dictionary<string, IDataLoader> _dataLoaders = new Dictionary<string, IDataLoader>();

  public ZSchemaResolver(IZContext context) : base(context) {
    Log.Information("[RES] new resolver {res} for {context} : {stack}", this, context.Root, new ZTrace(new StackTrace().ToString()).ToString());
  }

  public async Task<TData[]> LoadArray<TKey, TData>(
    string name, Func<IReadOnlyList<TKey>, Task<ILookup<TKey, TData>>> load, TKey? key, List<TData> existing
  ) where TKey : notnull where TData : class {
    Log.Information("[RES] {name} queue {key} with {res} in {context}", name, key, this, Context.Root);
    if (key == null) return new TData[] { };
    try {
      // IScope outerScope = Tracer.Instance.ActiveScope;
      // if (existing.Any()) return Task.FromResult(existing.ToArray());
      if (!name.EndsWith("[]")) name += "[]";
      IDataLoader<TKey, TData[]>? loader = _dataLoaders.TryGetValue(name, out var dataLoader) ?
        dataLoader as IDataLoader<TKey, TData[]> : null;
      if (loader == null) {
        _dataLoaders[name] = loader = await GroupDataLoader<TKey, TData>(name, async (keys, token) => {
          // using var op = new FurSpan("DB", name);
          // using var op = Context
          Log.Information("[RES] {name} begin {@keys} with {res} in {context}", name, keys, this, Context.Root);
          // return await Context.Data.ExecuteLocked(() => load(keys));
          return await load(keys);
        });
      }

      if (existing.Any()) {
        TData[] ret = existing.ToArray();
        loader.SetCacheEntry(key, ret);
        return ret;
      }

      return (await loader.LoadAsync(key) as TData[])!;
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
      IDataLoader<TKey, List<TData>>? loader = _dataLoaders.TryGetValue(name, out var dataLoader) ? dataLoader as IDataLoader<TKey, List<TData>> : null;
      if (loader == null) {
        _dataLoaders[name] = loader = await SingleDataLoader<TKey, List<TData>>(name, async (k, token) => (await load(k)).ToImmutableDictionary());
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
      IDataLoader<TKey, TData>? loader = _dataLoaders.TryGetValue(name, out var dataLoader) ? dataLoader as IDataLoader<TKey, TData> : null;
      if (loader == null) {
        _dataLoaders[name] = loader = await SingleDataLoader<TKey, TData>(name, async (k, token) => (await load(k)).ToImmutableDictionary());
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
      return (await loader.LoadAsync(keys.ToArray())).Where(v => v != null).Cast<TData>().ToImmutableList();
    } catch (Exception e) {
      if (!(e is TaskCanceledException)) Log.Warning(e, "[RES] failed to resolve all {name}", name);
      throw;
    }
    // TData[] ret = await LoadArray(name, load, key, existing == null ? new List<TData>() : new List<TData>() { existing });
    // return ret.FirstOrDefault();
  }


  private async Task<IDataLoader<TKey, TData>> SingleDataLoader<TKey, TData>(string key, FetchBatch<TKey, TData> fetch) where TKey : notnull where TData : class {
    if (key == null) throw new NullReferenceException(nameof(key));
    var services = Context.ServiceProvider!;
    var loader = services.GetRequiredService<DataLoaderRegistry>().SingleDataLoader(services, key, fetch);

    // Warm the cache with existing models from EFCore's change tracker
    var user = Context.CurrentIdentity?.IZUser;
    if (user != null && user.GetType().IsAssignableTo(typeof(TData))) {
      Log.Verbose("[DATA] special user: {user}", user);
      loader.SetCacheEntry(user.Id, user);
    }
    if (typeof(TData).IsSubclassOf(typeof(ModelKey<TKey>))) {
      var mems = await Context.Data.GetMemoryModels<TData>();
      foreach (var model in mems) {
        Log.Verbose("[DATA] provide {type} {model}", typeof(TData), model);
        loader.SetCacheEntry((model as ModelKey<TKey>)!.Id, model);
      }
    }
    return loader;
  }

  public async Task<IDataLoader<TKey, TData[]>> GroupDataLoader<TKey, TData>(string key, FetchGroup<TKey, TData> fetch) where TKey : notnull where TData : class {
    if (key == null) throw new NullReferenceException(nameof(key));
    var services = Context.ServiceProvider!;
    var loader = services.GetRequiredService<DataLoaderRegistry>().GroupDataLoader(services, key, fetch);

    // Warm the cache with existing models from EFCore's change tracker
    var user = Context.CurrentIdentity?.IZUser;
    if (user != null && user.GetType().IsAssignableTo(typeof(TData))) {
      Log.Verbose("[DATA] special user: {user}", user);
      loader.SetCacheEntry(user.Id, user);
    }
    if (typeof(TData).IsSubclassOf(typeof(ModelKey<TKey>))) {
      var mems = await Context.Data.GetMemoryModels<TData>();
      foreach (var model in mems) {
        Log.Verbose("[DATA] provide {type} {model}", typeof(TData), model);
        loader.SetCacheEntry((model as ModelKey<TKey>)!.Id, model);
      }
    }
    return loader;
  }

  public override void Dispose() {
    base.Dispose();
    // Log.Information("[DISP] Resolver for {keys}", _dataLoaders.Keys.ToList());
    _dataLoaders.Clear();
  }
}

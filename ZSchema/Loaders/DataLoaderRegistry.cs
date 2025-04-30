using System;
using System.Collections.Concurrent;
using GreenDonut;
using HotChocolate.Fetching;

namespace IZ.Schema.Loaders;

public class DataLoaderRegistry {
  private readonly ConcurrentDictionary<string, object> _groupLoaders = new ConcurrentDictionary<string, object>();

  public IDataLoader<TKey, TValue[]> GroupDataLoader<TKey, TValue>(IServiceProvider sp, string field, FetchGroup<TKey, TValue> fetch) where TKey : notnull {
    var key = typeof(TValue).Name + "." + field;
    var dl = (_groupLoaders.GetOrAdd(key, (k) =>
      new MultiDataLoader<TKey, TValue>(k, fetch, sp)
    )) ?? throw new ArgumentException($"Failed to create DataLoader {key}");

    return dl as IDataLoader<TKey, TValue[]> ??  throw new ArgumentException(
      $"Could not convert DataLoader {dl.GetType()} to IDataLoader<{typeof(TKey).Name}, {typeof(TValue).Name}> for {key}");
  }

  private readonly ConcurrentDictionary<string, object> _singleLoaders = new ConcurrentDictionary<string, object>();

  public IDataLoader<TKey, TValue> SingleDataLoader<TKey, TValue>(IServiceProvider sp, string field, FetchBatch<TKey, TValue> fetch) where TKey : notnull {
    var key = typeof(TValue).Name + "." + field;
    var dl = (_singleLoaders.GetOrAdd(key, (k) =>
      new SingleDataLoader<TKey, TValue>(k, fetch, sp)
    )) ?? throw new ArgumentException($"Failed to create DataLoader {key}");

    return dl as IDataLoader<TKey, TValue> ??  throw new ArgumentException(
      $"Could not convert DataLoader {dl.GetType()} to IDataLoader<{typeof(TKey).Name}, {typeof(TValue).Name}> for {key}");
  }
}

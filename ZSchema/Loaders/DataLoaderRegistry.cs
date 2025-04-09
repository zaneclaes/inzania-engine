using System;
using System.Collections.Concurrent;
using GreenDonut;
using HotChocolate.Fetching;

namespace IZ.Schema.Loaders;

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

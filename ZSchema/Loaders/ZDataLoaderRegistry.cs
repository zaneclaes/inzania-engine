using System;
using System.Collections.Concurrent;
using HotChocolate.Fetching;
using IZ.Core.Contexts;

namespace IZ.Schema.Loaders;

public class ZDataLoaderRegistry {
  private readonly ConcurrentDictionary<string, IZDataLoader> _groupLoaders = new ConcurrentDictionary<string, IZDataLoader>();

  private readonly ConcurrentDictionary<string, IZDataLoader> _singleLoaders = new ConcurrentDictionary<string, IZDataLoader>();

  public IZDataLoader<TKey, TValue[]> GroupDataLoader<TKey, TValue>(IZContext context, string field, FetchGroup<TKey, TValue> fetch) where TKey : notnull {
    string key = typeof(TValue).Name + "." + field;
    var dl = _groupLoaders.GetOrAdd(key, k =>
      new MultiDataLoader<TKey, TValue>(context, k, fetch)
    ) ?? throw new ArgumentException($"Failed to create DataLoader {key}");
    // var dl = new MultiDataLoader<TKey, TValue>(key, fetch, sp);

    return dl as IZDataLoader<TKey, TValue[]> ?? throw new ArgumentException(
      $"Could not convert DataLoader {dl.GetType()} to IDataLoader<{typeof(TKey).Name}, {typeof(TValue).Name}> for {key}");
  }

  public IZDataLoader<TKey, TValue> SingleDataLoader<TKey, TValue>(IZContext context, string field, FetchBatch<TKey, TValue> fetch) where TKey : notnull {
    string key = typeof(TValue).Name + "." + field;
    var dl = _singleLoaders.GetOrAdd(key, k =>
      new SingleDataLoader<TKey, TValue>(context, k, fetch)
    ) ?? throw new ArgumentException($"Failed to create DataLoader {key}");
    // var dl = new SingleDataLoader<TKey, TValue>(key, fetch, sp);

    return dl as IZDataLoader<TKey, TValue> ?? throw new ArgumentException(
      $"Could not convert DataLoader {dl.GetType()} to IDataLoader<{typeof(TKey).Name}, {typeof(TValue).Name}> for {key}");
  }
}

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GreenDonut;
using HotChocolate.Execution;
using HotChocolate.Execution.Processing;
using HotChocolate.Fetching;
using HotChocolate.Resolvers;
using IZ.Core.Contexts;
using IZ.Core.Data;
using IZ.Core.Observability.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace IZ.Schema.Loaders;

public class ZDataLoaderRegistry {
  private readonly ConcurrentDictionary<string, IZDataLoader> _groupLoaders = new ConcurrentDictionary<string, IZDataLoader>();

  public IZDataLoader<TKey, TValue[]> GroupDataLoader<TKey, TValue>(IZContext context, string field, FetchGroup<TKey, TValue> fetch) where TKey : notnull {
    var key = typeof(TValue).Name + "." + field;
    var dl = (_groupLoaders.GetOrAdd(key, (k) =>
      new MultiDataLoader<TKey, TValue>(context, k, fetch)
    )) ?? throw new ArgumentException($"Failed to create DataLoader {key}");
    // var dl = new MultiDataLoader<TKey, TValue>(key, fetch, sp);

    return dl as IZDataLoader<TKey, TValue[]> ??  throw new ArgumentException(
      $"Could not convert DataLoader {dl.GetType()} to IDataLoader<{typeof(TKey).Name}, {typeof(TValue).Name}> for {key}");
  }

  private readonly ConcurrentDictionary<string, IZDataLoader> _singleLoaders = new ConcurrentDictionary<string, IZDataLoader>();

  public IZDataLoader<TKey, TValue> SingleDataLoader<TKey, TValue>(IZContext context, string field, FetchBatch<TKey, TValue> fetch) where TKey : notnull {
    var key = typeof(TValue).Name + "." + field;
    var dl = (_singleLoaders.GetOrAdd(key, (k) =>
      new SingleDataLoader<TKey, TValue>(context, k, fetch)
    )) ?? throw new ArgumentException($"Failed to create DataLoader {key}");
    // var dl = new SingleDataLoader<TKey, TValue>(key, fetch, sp);

    return dl as IZDataLoader<TKey, TValue> ??  throw new ArgumentException(
      $"Could not convert DataLoader {dl.GetType()} to IDataLoader<{typeof(TKey).Name}, {typeof(TValue).Name}> for {key}");
  }
}

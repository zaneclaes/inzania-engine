#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GreenDonut;
using HotChocolate.Fetching;
using Microsoft.Extensions.DependencyInjection;

#endregion

namespace IZ.Schema.Loaders;

public class SingleDataLoader<TKey, TValue>
  : BatchDataLoader<TKey, TValue>, GreenDonut.IDataLoader<TKey, TValue>
  where TKey : notnull {
  private readonly FetchBatch<TKey, TValue> _fetch;

  public SingleDataLoader(
    string key,
    FetchBatch<TKey, TValue> fetch,
    IServiceProvider sp)
    : base(sp.GetRequiredService<IBatchScheduler>(), sp.GetRequiredService<DataLoaderOptions>()) {
    _fetch = fetch ?? throw new ArgumentNullException(nameof(fetch));
    CacheKeyType = $"{GetCacheKeyType(GetType())}-{key}";
  }

  protected override string CacheKeyType { get; }

  protected override Task<IReadOnlyDictionary<TKey, TValue>> LoadBatchAsync(
    IReadOnlyList<TKey> keys,
    CancellationToken cancellationToken) =>
    _fetch(keys, cancellationToken);
}

internal sealed class MultiDataLoader<TKey, TValue>
  : GroupedDataLoader<TKey, TValue>, GreenDonut.IDataLoader<TKey, TValue[]>
  where TKey : notnull {
  private readonly FetchGroup<TKey, TValue> _fetch;

  public MultiDataLoader(
    string key,
    FetchGroup<TKey, TValue> fetch,
    IServiceProvider sp)
    : base(sp.GetRequiredService<IBatchScheduler>(), sp.GetRequiredService<DataLoaderOptions>()) {
    _fetch = fetch ?? throw new ArgumentNullException(nameof(fetch));
    CacheKeyType = $"{GetCacheKeyType(GetType())}-{key}";
  }

  protected override string CacheKeyType { get; }

  protected override Task<ILookup<TKey, TValue>> LoadGroupedBatchAsync(
    IReadOnlyList<TKey> keys,
    CancellationToken cancellationToken) =>
    _fetch(keys, cancellationToken);

}

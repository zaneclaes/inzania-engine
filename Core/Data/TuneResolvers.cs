#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

#endregion

namespace IZ.Core.Data;

public static class TuneResolvers {

  public static async Task<TData> LoadRequired<TKey, TData>(
    this ITuneResolver resolver, string name, Func<IReadOnlyList<TKey>, Task<Dictionary<TKey, TData>>> load, TKey key, TData? existing, Func<TData, TKey> fetchKey
  ) where TKey : notnull {
    var ret = await resolver.LoadOptional(name, load, key, existing, fetchKey);
    return ret ?? throw new ArgumentException($"Missing {typeof(TData).Name} for {key}");
  }

  public static async Task<TData?> LoadOptional<TKey, TData>(
    this ITuneResolver resolver, string name,
    Func<IReadOnlyList<TKey>, Task<Dictionary<TKey, TData>>> load, TKey? key, TData? existing, Func<TData, TKey> fetchKey
  ) where TKey : notnull {
    if (key == null) return default;
    return (
      await resolver.LoadAll(name, load, new List<TKey> {
          key
        },
        existing == null ? new List<TData>() : new List<TData> {
          existing
        }, fetchKey)
      ).FirstOrDefault();
  }
}

#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using IZ.Core.Contexts;

#endregion

namespace IZ.Core.Data;

public class TuneDefaultResolver : LogicBase, ITuneResolver {
  public async Task<TData[]> LoadArray<TKey, TData>(
    string name, Func<IReadOnlyList<TKey>, Task<ILookup<TKey, TData>>> load, TKey? key, List<TData> existing
  ) where TKey : notnull {
    if (key == null) return new TData[] { };
    ILookup<TKey, TData> loaded = await load(new List<TKey> {
      key
    });
    return loaded[key].ToArray();
  }

  public async Task<IReadOnlyList<TData>> LoadAll<TKey, TData>(
    string name, Func<IReadOnlyList<TKey>, Task<Dictionary<TKey, TData>>> load,
    List<TKey> keys, List<TData> existing, Func<TData, TKey> fetchKey
  ) where TKey : notnull {
    IZEnv.Log.Information("LoadAll {name} x{count}", name, keys.Count);
    if (!keys.Any()) return new List<TData>();
    Dictionary<TKey, TData> loaded = await load(keys);
    return loaded.Keys.Where(k => loaded.ContainsKey(k)).Select(k => loaded[k]).Where(v => v != null).ToList();
  }

  public async Task<IReadOnlyList<TData>> LoadMany<TKey, TData>(
    string name, Func<IReadOnlyList<TKey>, Task<Dictionary<TKey, List<TData>>>> load, List<TKey> keys, List<TData> existing, Func<TData, TKey> fetchKey
  ) where TKey : notnull {
    IZEnv.Log.Information("LoadAll {name} x{count}", name, keys.Count);
    if (!keys.Any()) return new List<TData>();
    Dictionary<TKey, List<TData>> loaded = await load(keys);
    return loaded.Keys.Where(k => loaded.ContainsKey(k)).SelectMany(k => loaded[k]).Where(v => v != null).ToList();
  }

  public TuneDefaultResolver(ITuneContext context) : base(context) {
    Log.Information("[RES] DEFAULT resolver");
  }
}

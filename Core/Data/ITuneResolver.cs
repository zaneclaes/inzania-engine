#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using IZ.Core.Contexts;
using IZ.Core.Data.Attributes;

#endregion

namespace IZ.Core.Data;

[ApiDocs("Implements deferred loading of objects when API is executed thru a Request, batching requests together")]
public interface ITuneResolver : IHaveContext {
  public Task<TData[]> LoadArray<TKey, TData>(
    string name, Func<IReadOnlyList<TKey>, Task<ILookup<TKey, TData>>> load, TKey? key, List<TData> existing
  ) where TKey : notnull;

  public Task<IReadOnlyList<TData>> LoadAll<TKey, TData>(
    string name, Func<IReadOnlyList<TKey>, Task<Dictionary<TKey, TData>>> load, List<TKey> keys, List<TData> existing, Func<TData, TKey> fetchKey
  ) where TKey : notnull;

  public Task<IReadOnlyList<TData>> LoadMany<TKey, TData>(
    string name, Func<IReadOnlyList<TKey>, Task<Dictionary<TKey, List<TData>>>> load, List<TKey> keys, List<TData> existing, Func<TData, TKey> fetchKey
  ) where TKey : notnull;
}

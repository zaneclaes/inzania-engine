#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using IZ.Core.Api.Types;
using IZ.Core.Contexts;
using IZ.Core.Utils;

#endregion

namespace IZ.Core.Data;

public class Resolution<T> where T : DataObject {
  private Task<T?> _resolver;

  public Task<T?> Optional() => _resolver;

  public async Task<T> Required() {
    var ret = await _resolver;
    return ret ?? throw new NullReferenceException(typeof(T).Name);
  }

  public Resolution(Task<T?> resolver) {
    _resolver = resolver;
  }
}

public abstract class ApiObject : ContextualObject {
  protected ApiObject(IZContext? context = null) : base(context) {
    context?.Log.Verbose("[API] {obj} context = {root}", GetType().Name, context?.Root);
  }

  protected override string ContextualObjectGroup => "Object";

  protected Resolution<TData> ResolveLocalId<TData>(
    string localPropName,
    Func<IZQueryable<TData>, IZQueryable<TData>>? beforeFilter = null, Func<IZQueryable<TData>, IZQueryable<TData>>? afterFilter = null
  ) where TData : ModelKey<string> => new Resolution<TData>(ResolveLocalKey<string, TData>(localPropName, null, beforeFilter, afterFilter));

  protected Resolution<TData> ResolveLocalId<TData>(
    string? key, string localPropName,
    Func<IZQueryable<TData>, IZQueryable<TData>>? beforeFilter = null, Func<IZQueryable<TData>, IZQueryable<TData>>? afterFilter = null
  ) where TData : ModelKey<string> => new Resolution<TData>(ResolveKey(key, localPropName, null, beforeFilter, afterFilter));

  // protected async Task<TData> ResolveRequiredId<TData>(
  //   string localPropName,
  //   Func<IZQueryable<TData>, IZQueryable<TData>>? beforeFilter = null, Func<IZQueryable<TData>, IZQueryable<TData>>? afterFilter = null
  // ) where TData : ModelKey<string> => await ResolveOptionalId(localPropName, beforeFilter, afterFilter) ?? throw new NullReferenceException(localPropName);

  // protected async Task<TData> ResolveRequiredId<TData>(
  //   string localPropName,
  //   Func<IZQueryable<TData>, IZQueryable<TData>>? beforeFilter = null, Func<IZQueryable<TData>, IZQueryable<TData>>? afterFilter = null
  // ) where TData : ModelKey<string> =>
  //   await ResolveOptionalProp<string, TData>(localPropName, null, beforeFilter, afterFilter) ?? throw new NullReferenceException(localPropName);

  // protected async Task<TData> ResolveRequiredId<TKey, TData>(
  //   TKey localId, string localPropName, string? foreignKeyPropName = null,
  //   Func<IZQueryable<TData>, IZQueryable<TData>>? beforeFilter = null, Func<IZQueryable<TData>, IZQueryable<TData>>? afterFilter = null
  // ) where TData : ModelKey<TKey> where TKey : notnull =>
  //   await ResolveOptionalId(localId, localPropName, foreignKeyPropName, beforeFilter, afterFilter) ?? throw new NullReferenceException(localPropName);

  // private async Task<TData> ResolveRequiredProp<TKey, TData>(
  //   string localPropName, string? foreignKeyPropName = null,
  //   Func<IZQueryable<TData>, IZQueryable<TData>>? beforeFilter = null, Func<IZQueryable<TData>, IZQueryable<TData>>? afterFilter = null
  // ) where TData : ModelKey<TKey> where TKey : notnull =>
  //   await ResolveOptionalProp<TKey, TData>(localPropName, foreignKeyPropName, beforeFilter, afterFilter) ?? throw new NullReferenceException(localPropName);

  private Task<TData?> ResolveLocalKey<TKey, TData>(
    string localPropName, string? foreignKeyPropName = null,
    Func<IZQueryable<TData>, IZQueryable<TData>>? beforeFilter = null, Func<IZQueryable<TData>, IZQueryable<TData>>? afterFilter = null
  ) where TData : ModelKey<TKey> where TKey : notnull {

    string localIdFieldName = (localPropName + "Id").ToFieldName();
    if (!ApiType.ObjectDescriptor.ScalarProperties.ContainsKey(localIdFieldName))
      throw new ArgumentException($"Scalar ID Key '{localIdFieldName}' missing: {ApiType.ObjectDescriptor} among {ApiType.ObjectDescriptor.ScalarProperties}");
    var localIdProp = ApiType.ObjectDescriptor.ScalarProperties[localIdFieldName];

    return ResolveKey((TKey) localIdProp.GetValue(this)!, localPropName, foreignKeyPropName, beforeFilter, afterFilter);
  }

  private IZQueryable<TData> CreateQuery<TKey, TData>(
    string foreignKeyPropName, IReadOnlyList<TKey> keys, Func<IZQueryable<TData>, IZQueryable<TData>>? beforeFilter, Func<IZQueryable<TData>, IZQueryable<TData>>? afterFilter
  ) where TData : DataObject where TKey : notnull {
    IZQueryable<TData> query = Context.QueryFor<TData>();
    if (beforeFilter != null) query = beforeFilter(query);
    query = query.FilterKeyIn(foreignKeyPropName, keys.ToArray());
    if (afterFilter != null) query = afterFilter(query);
    return query;
  }

  // protected Task<TData?> ResolveOptionalId<TData>(
  //   string? localId, string localPropName, string? foreignKeyPropName = null,
  //   Func<IZQueryable<TData>, IZQueryable<TData>>? beforeFilter = null, Func<IZQueryable<TData>, IZQueryable<TData>>? afterFilter = null
  // ) where TData : ModelKey<string> => ResolveOptionalId<string, TData>(localId, localPropName, foreignKeyPropName, beforeFilter, afterFilter);

  protected async Task<TData?> ResolveKey<TKey, TData>(
    TKey? localId, string localPropName, string? foreignKeyPropName = null,
    Func<IZQueryable<TData>, IZQueryable<TData>>? beforeFilter = null, Func<IZQueryable<TData>, IZQueryable<TData>>? afterFilter = null
  ) where TData : ModelKey<TKey> where TKey : notnull {
    foreignKeyPropName ??= "Id";
    var (localProp, foreignProp) = ResolvePropertyMap<TData>(localPropName, foreignKeyPropName);

    // Log.Information("FIELD {name} opt", localFieldName);
    var existing = localProp.GetValue(this) as TData;
    // if (existing == null) {
    //   var memModels = await Context.Data.GetMemoryModels<TData>();
    //   existing = memModels.FirstOrDefault(m => m.Id.Equals(localId));
    //   Log.Information("[RESOLVE] {type}#{id} => {existing}", typeof(TData), localId, existing);
    // }
    var ret = await Context.Resolver.LoadOptional(localProp.FieldName, async keys =>
        await CreateQuery(foreignKeyPropName, keys, beforeFilter, afterFilter).LoadDictionaryAsync(l => (TKey) foreignProp.GetValue(l)!),
      localId, existing, o => (TKey) foreignProp.GetValue(o)!);
    localProp.SetValue(this, ret);
    return ret;
  }

  // protected async Task<TData?> ResolveForeignProp<TKey, TData>(
  //   string localPropName, string foreignPropName,
  //   Func<IZQueryable<TData>, IZQueryable<TData>>? beforeFilter = null, Func<IZQueryable<TData>, IZQueryable<TData>>? afterFilter = null
  // ) where TData : ModelKey<TKey> where TKey : notnull {
  //   var (localProp, foreignProp) = ResolvePropertyMap<TData>(localPropName, foreignPropName);
  //
  //   // Log.Information("FIELD {name} opt", localFieldName);
  //   var existing = localProp.GetValue(this) as TData;
  //   // if (existing == null) {
  //   //   var memModels = await Context.Data.GetMemoryModels<TData>();
  //   //   existing = memModels.FirstOrDefault(m => m.Id.Equals(localId));
  //   //   Log.Information("[RESOLVE] {type}#{id} => {existing}", typeof(TData), localId, existing);
  //   // }
  //   var ret = await Context.Resolver.LoadOptional(localProp.FieldName, async keys =>
  //       await CreateQuery(foreignPropName, keys, beforeFilter, afterFilter).LoadDictionaryAsync(l => (TKey) foreignProp.GetValue(l)!),
  //     localId, existing, o => (TKey) foreignProp.GetValue(o)!);
  //   localProp.SetValue(this, ret);
  //   return ret;
  // }

  protected Tuple<ZPropertyDescriptor, ZPropertyDescriptor> ResolvePropertyMap<TData>(
    string localArrayPropName, string foreignKeyName
  ) where TData : DataObject {

    string localFieldName = localArrayPropName.ToFieldName();
    var localProp = ApiType.ObjectDescriptor.GetProperty(localFieldName) ??
                    throw new ArgumentException($"Local Key '{localFieldName}' missing: {ApiType.ObjectDescriptor}");

    var foreignDesc = ZTypeDescriptor.FromType(typeof(TData));
    string foreignFieldName = foreignKeyName.ToFieldName();
    var foreignProp = foreignDesc.ObjectDescriptor.GetProperty(foreignFieldName) ??
                      throw new ArgumentException($"Foreign Key '{foreignFieldName}' missing: {foreignDesc.ObjectDescriptor}");

    return new Tuple<ZPropertyDescriptor, ZPropertyDescriptor>(localProp, foreignProp);
  }
  //
  // protected async Task<TData[]> ResolveArray<TData>(string localId, string localArrayPropName, string foreignKeyName) where TData : DataObject {
  //   var (localArrayProp, foreignProp) = ResolvePropertyMap<TData>(localArrayPropName, foreignKeyName);
  //
  //   var existing = (localArrayProp.GetValue(this) as IEnumerable<TData>)?.ToList() ?? new List<TData>();
  //   var ret = await Context.Resolver.LoadArray<string, TData>(localArrayProp.FieldName, async (keys) =>
  //     await Context.QueryFor<TData>()
  //       .WhereKeyIn(foreignKeyName, keys.ToArray())
  //       .LoadLookupAsync(l => (string)foreignProp.GetValue(l)!), localId, existing);
  //   localArrayProp.SetValue(this, ret.ToList());
  //   return ret;
  // }

  protected Task<TData[]> ResolveArray<TData>(
    string localId, string localArrayPropName, string? foreignKeyName = null,
    Func<IZQueryable<TData>, IZQueryable<TData>>? beforeFilter = null, Func<IZQueryable<TData>, IZQueryable<TData>>? afterFilter = null
  ) where TData : DataObject => ResolveArrayItems(localId, localArrayPropName, foreignKeyName, beforeFilter, afterFilter);

  private async Task<TData[]> ResolveArrayItems<TKey, TData>(
    TKey localId, string localArrayPropName, string? foreignKeyName = null,
    Func<IZQueryable<TData>, IZQueryable<TData>>? beforeFilter = null, Func<IZQueryable<TData>, IZQueryable<TData>>? afterFilter = null
  ) where TData : DataObject where TKey : notnull {
    foreignKeyName ??= "Id";
    var (localArrayProp, foreignProp) = ResolvePropertyMap<TData>(localArrayPropName, foreignKeyName);

    List<TData> existing = (localArrayProp.GetValue(this) as IEnumerable<TData>)?.ToList() ?? new List<TData>();
    TData[] ret = await Context.Resolver.LoadArray(localArrayProp.FieldName, async keys =>
      await CreateQuery(foreignKeyName, keys, beforeFilter, afterFilter)
        .LoadLookupAsync(l => (TKey) foreignProp.GetValue(l)!), localId, existing);
    localArrayProp.SetValue(this, ret.ToList());
    return ret;
  }
}

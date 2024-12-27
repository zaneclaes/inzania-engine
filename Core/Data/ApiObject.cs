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

public abstract class ApiObject : ContextualObject {
  protected ApiObject(ITuneContext? context = null) : base(context) { }

  protected override string ContextualObjectGroup => "Object";

  protected async Task<TData> ResolveRequiredId<TKey, TData>(
    TKey localId, string localPropName, string foreignPropName, ITuneQueryable<TData>? q = null
  ) where TData : ModelKey<TKey> where TKey : notnull =>
    await ResolveOptionalId(localId, localPropName, foreignPropName, q) ?? throw new NullReferenceException(nameof(localPropName));

  protected async Task<TData> ResolveRequiredProp<TKey, TData>(
    string localPropName, string? foreignPropName = null, ITuneQueryable<TData>? q = null
  ) where TData : ModelKey<TKey> where TKey : notnull =>
    await ResolveOptionalProp<TKey, TData>(localPropName, foreignPropName, q) ?? throw new NullReferenceException(nameof(localPropName));

  protected Task<TData?> ResolveOptionalProp<TKey, TData>(
    string localPropName, string? foreignPropName = null, ITuneQueryable<TData>? q = null
  ) where TData : ModelKey<TKey> where TKey : notnull {

    string localIdFieldName = (localPropName + "Id").ToFieldName();
    if (!ApiType.ObjectDescriptor.ScalarProperties.ContainsKey(localIdFieldName))
      throw new ArgumentException($"Scalar ID Key '{localIdFieldName}' missing: {ApiType.ObjectDescriptor} among {ApiType.ObjectDescriptor.ScalarProperties}");
    var localIdProp = ApiType.ObjectDescriptor.ScalarProperties[localIdFieldName];

    return ResolveOptionalId((TKey) localIdProp.GetValue(this)!, localPropName, foreignPropName, q);
  }

  protected async Task<TData?> ResolveOptionalId<TKey, TData>(
    TKey? localId, string localPropName, string? foreignPropName = null, ITuneQueryable<TData>? q = null
  ) where TData : ModelKey<TKey> where TKey : notnull {
    foreignPropName ??= "Id";
    var (localProp, foreignProp) = ResolvePropertyMap<TData>(localPropName, foreignPropName);

    // Log.Information("FIELD {name} opt", localFieldName);
    var existing = localProp.GetValue(this) as TData;
    var ret = await Context.Resolver.LoadOptional(localProp.FieldName, async keys =>
        await (q ?? Context.QueryFor<TData>())
          .FilterKeyIn(foreignPropName, keys.ToArray())
          .LoadDictionaryAsync(l => (TKey) foreignProp.GetValue(l)!),
      localId, existing, o => (TKey) foreignProp.GetValue(o)!);
    localProp.SetValue(this, ret);
    return ret;
  }

  protected Tuple<TunePropertyDescriptor, TunePropertyDescriptor> ResolvePropertyMap<TData>(
    string localArrayPropName, string foreignKeyName
  ) where TData : DataObject {

    string localFieldName = localArrayPropName.ToFieldName();
    var localProp = ApiType.ObjectDescriptor.GetProperty(localFieldName) ??
                    throw new ArgumentException($"Local Key '{localFieldName}' missing: {ApiType.ObjectDescriptor}");

    var foreignDesc = TuneTypeDescriptor.FromType(typeof(TData));
    string foreignFieldName = foreignKeyName.ToFieldName();
    var foreignProp = foreignDesc.ObjectDescriptor.GetProperty(foreignFieldName) ??
                      throw new ArgumentException($"Foreign Key '{foreignFieldName}' missing: {foreignDesc.ObjectDescriptor}");

    return new Tuple<TunePropertyDescriptor, TunePropertyDescriptor>(localProp, foreignProp);
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
    string localId, string localArrayPropName, string? foreignKeyName = null, ITuneQueryable<TData>? q = null
  ) where TData : DataObject => ResolveArrayItems(localId, localArrayPropName, foreignKeyName, q);

  protected async Task<TData[]> ResolveArrayItems<TKey, TData>(
    TKey localId, string localArrayPropName, string? foreignKeyName = null, ITuneQueryable<TData>? q = null
  ) where TData : DataObject where TKey : notnull {
    foreignKeyName ??= "Id";
    var (localArrayProp, foreignProp) = ResolvePropertyMap<TData>(localArrayPropName, foreignKeyName);

    List<TData>? existing = (localArrayProp.GetValue(this) as IEnumerable<TData>)?.ToList() ?? new List<TData>();
    TData[]? ret = await Context.Resolver.LoadArray(localArrayProp.FieldName, async keys =>
      await (q ?? Context.QueryFor<TData>())
        .FilterKeyIn(foreignKeyName, keys.ToArray())
        .LoadLookupAsync(l => (TKey) foreignProp.GetValue(l)!), localId, existing);
    localArrayProp.SetValue(this, ret.ToList());
    return ret;
  }
}

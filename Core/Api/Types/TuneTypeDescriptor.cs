#region

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using IZ.Core.Contexts;
using IZ.Core.Utils;

#endregion

namespace IZ.Core.Api.Types;

// Describes wrapping type around a TuneObjectDescriptor (nullable, list, etc.)
public class TuneTypeDescriptor {
  public Type OrigType { get; set; } = default!;

  public bool HasInner => IsList;

  public bool IsList { get; set; }

  public bool IsNullableOuter { get; set; }

  public bool IsNullableInner { get; set; }

  public TuneObjectDescriptor ObjectDescriptor { get; set; } = default!;

  public string ToObjectTypeName(bool asInput, string optionalIndicator = "") {
    string ret = asInput ? ObjectDescriptor.InputTypeName : ObjectDescriptor.TypeName;
    if (HasInner) ret += IsNullableInner ? optionalIndicator : "!";
    if (IsList) ret = $"[{ret}]";
    ret += IsNullableOuter ? optionalIndicator : "!";
    return ret;
  }

  internal static readonly Dictionary<string, TuneTypeDescriptor> ApiTypes = new Dictionary<string, TuneTypeDescriptor>();

  public static TuneTypeDescriptor FromType(Type t, bool isOptional = false) {
    string key = $"{t}{(isOptional ? "?" : "!")}";
    if (ApiTypes.TryGetValue(key, out var d)) return d;

    var innerType = StripIgnoredOuterTypes(t);
    var ret = new TuneTypeDescriptor();
    IZEnv.Log.Verbose("[TYPE] start {t}", innerType.Name);
    ret.OrigType = innerType;
    var nt1 = Nullable.GetUnderlyingType(innerType);
    if (nt1 != null) {
      innerType = nt1;
      ret.IsNullableOuter = true;
    } else if (isOptional) {
      ret.IsNullableOuter = true;
    }
    if (innerType.IsArray) {
      IZEnv.Log.Verbose("[TYPE] array {t}", innerType.Name);
      innerType = innerType.GetElementType()!;
      ret.IsList = true;
    } else if (innerType.HasAssignableType(typeof(IList))) {
      IZEnv.Log.Verbose("[TYPE] list {t}", innerType.Name);
      innerType = innerType.GenericTypeArguments[0];
      ret.IsList = true;
    }
    var nt2 = Nullable.GetUnderlyingType(innerType);
    if (ret.IsList && nt2 != null) {
      IZEnv.Log.Verbose("[TYPE] list-nullable {t}", innerType.Name);
      innerType = nt2;
      ret.IsNullableInner = true;
    }
    ret.ObjectDescriptor = TuneObjectDescriptor.LoadTuneObjectDescriptor(innerType);
    // if (task) t = typeof(Task<>).MakeGenericType(t);
    ApiTypes[key] = ret;
    return ret;
  }

  private static Type StripIgnoredOuterTypes(Type t) {
    if (t.Name == "Task`1") { // ISAssignableTo(Task<>) seems to not work
      t = t.GenericTypeArguments[0];
    }
    if (t.HasAssignableType(typeof(ITuneResult))) {
      // IZEnv.Log.Information("T {old} -> {new}", t.Name, t.GenericTypeArguments[0].Name);
      t = t.GenericTypeArguments[0];
    }
    return t;
  }

  public static void ExpandTypeTree(params TuneTypeDescriptor[] types) {
    ExpandTypeTree(ApiTypes.Values.Union(types).Distinct().ToList(), new List<TuneTypeDescriptor>());
  }

  private static void ExpandTypeTree(List<TuneTypeDescriptor> baseTypes, List<TuneTypeDescriptor> breadcrumbs) {
    List<TuneTypeDescriptor> added = new List<TuneTypeDescriptor>();
    foreach (var desc in baseTypes) {
      added.AddRange(desc.ExpandTypeTree(breadcrumbs));
    }
    if (added.Any()) ExpandTypeTree(added, breadcrumbs);
  }

  private List<TuneTypeDescriptor> ExpandTypeTree(List<TuneTypeDescriptor> breadcrumbs) {
    List<TuneTypeDescriptor> added = new List<TuneTypeDescriptor>();
    foreach (string? key in ObjectDescriptor.FieldMap.Keys) {
      // IZEnv.Log.Information("[EXPAND] TREE {type} :: {key} => {field}", this, key, ObjectDescriptor.FieldMap[key]);
      added.AddRange(ObjectDescriptor.FieldMap[key].ExpandTypes(breadcrumbs));
    }
    return added;
  }

  // public static Type MakeBaseType(Type t) {
  //   t = StripIgnoredOuterTypes(t);
  //   if (t.HasAssignableType(typeof(Nullable))) {
  //     t = t.GenericTypeArguments[0];
  //   }
  //   if (t.IsArray) {
  //     t = t.GetElementType()!;
  //   }
  //   if (t.HasAssignableType(typeof(IList))) {
  //     t = t.GenericTypeArguments[0];
  //   }
  //   // if (t.HasAssignableType(typeof(Nullable))) {
  //   //   t = t.GenericTypeArguments[0];
  //   // }
  //   while (t.GenericTypeArguments.Any()) {
  //     t = t.GenericTypeArguments[0];
  //   }
  //   return t;
  // }

  public override string ToString() => ToObjectTypeName(false, "?");
}

#region

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using IZ.Core.Contexts;
using IZ.Core.Utils;

#endregion

namespace IZ.Core.Api.Types;

// Describes wrapping type around a ZObjectDescriptor (nullable, list, etc.)
public class ZTypeDescriptor {

  public Type OrigType { get; set; } = null!;

  public bool HasInner => IsList || IsArray || DictionaryKeyType != null;

  public bool IsList { get; set; }

  public bool IsArray { get; set; }

  public bool IsNullableOuter { get; set; }

  public bool IsNullableInner { get; set; }

  public Type? DictionaryKeyType { get; set; }

  public ZObjectDescriptor ObjectDescriptor { get; set; } = null!;

  public string ToGraphTypeName(bool asInput, string optionalIndicator = "") {
    string ret = asInput ? ObjectDescriptor.InputTypeName : ObjectDescriptor.TypeName;
    if (HasInner) ret += IsNullableInner ? optionalIndicator : "!";
    if (IsList) ret = $"[{ret}]";
    ret += IsNullableOuter ? optionalIndicator : "!";
    return ret;
  }

  // public static List<ZTypeDescriptor> ExpandTypeTree(params ZTypeDescriptor[] types) => ExpandTypeTree(ApiTypes.Values.Union(types).Distinct().ToList(), new HashSet<ZTypeDescriptor>());
  public static List<ZTypeDescriptor> ExpandTypeTree(IZTypeMap typeMap, params ZTypeDescriptor[] types) {
    // breadcrumbs = "seen" (base + discovered)
    var breadcrumbs = new HashSet<ZTypeDescriptor>();

    // work queue
    var queue = new Queue<ZTypeDescriptor>(capacity: typeMap.ApiTypes.Count + types.Length);

    // seed from ApiTypes.Values
    foreach (var t in typeMap.ApiTypes.Values) {
      if (breadcrumbs.Add(t))
        queue.Enqueue(t);
    }

    // seed from params
    for (int i = 0; i < types.Length; i++)
    {
      var t = types[i];
      if (breadcrumbs.Add(t))
        queue.Enqueue(t);
    }

    // collect everything we *discover beyond the seed* (matches your previous return semantics better than returning the whole tree)
    var discovered = new List<ZTypeDescriptor>(256);

    while (queue.Count > 0)
    {
      var desc = queue.Dequeue();

      // Expand children; this should NOT allocate much
      foreach (var child in desc.EnumerateTypeTreeChildren(typeMap, breadcrumbs)) {
        // child was already added to breadcrumbs inside EnumerateTypeTreeChildren via Add/ExpandTypes,
        // so just enqueue + record
        queue.Enqueue(child);
        discovered.Add(child);
      }
    }

    return discovered;
  }
// Enumerates ONLY newly-added children (i.e., not already in breadcrumbs)
  private IEnumerable<ZTypeDescriptor> EnumerateTypeTreeChildren(IZTypeMap typeMap, HashSet<ZTypeDescriptor> breadcrumbs)
  {
    // Fast dictionary iteration (no Keys + indexer)
    foreach (var kvp in ObjectDescriptor.FieldMap)
    {
      var key = kvp.Key;
      var field = kvp.Value;

      // IMPORTANT: ExpandTypes should be the optimized version:
      // ExpandTypes(ISet<ZTypeDescriptor>) using breadcrumbs.Add(desc)
      var newly = field.ExpandTypes(breadcrumbs);

      // Avoid foreach over empty list allocations if your ExpandTypes returns a shared empty list
      for (int i = 0; i < newly.Count; i++)
        yield return newly[i];
    }

    // Polymorphic types:
    // Only yield if truly new; otherwise you spam the queue with repeats.
    foreach (var type in ObjectDescriptor.PolymorphicTypes) {
      var td = typeMap.LoadTypeDescriptor(type);
      if (breadcrumbs.Add(td))
        yield return td;
    }
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

  public string ToSystemTypeName() {
    var t = ObjectDescriptor.TypeName;
    if (IsNullableInner) t = $"Nullable<{t}>";
    if (IsArray) t = $"{t}[]";
    else if (IsList) t = $"List<{t}>";
    if (DictionaryKeyType != null) t = $"Dictionary<{DictionaryKeyType.Name}, {t}>";
    if (IsNullableOuter) t = $"Nullable<{t}>";
    return t;
  }

  internal string ToCast(string val) => !IsList && !IsNullableInner && !IsNullableOuter && (OrigType.IsEnum || ObjectDescriptor.IsScalar) ?
    $"({OrigType.Name}) {val}!" : $"({val} as {ToSystemTypeName()})!";

  public override string ToString() => ToGraphTypeName(false, "?");
}

#region

using System;
using System.Collections.Generic;
using System.Linq;
using IZ.Core.Api.Types;

#endregion

namespace IZ.Core.Parsing;

public static class Packing {
  public static string PackDictionaryAsQueryParams<TA, TB>(this Dictionary<TA, TB> dict) where TA : notnull => dict.PackDictionary("=", "&", false);
  public static string PackDictionaryAsId<TA, TB>(this Dictionary<TA, TB> dict) where TA : notnull => dict.PackDictionary("_", "-", false);
  public static string PackDictionary<TA, TB>(this Dictionary<TA, TB> dict, string inner = ":", string outer = ",", bool pad = true) where TA : notnull =>
    (pad ? outer : "") + string.Join(outer, dict.Keys.Select(k => $"{k}{inner}{dict[k]}")) + (pad ? outer : "");

  public static Dictionary<TA, TB> UnpackDictionary<TA, TB>(string val, string inner = ":", string outer = ",") where TA : notnull =>
    val.Split(outer).Where(s => !string.IsNullOrWhiteSpace(s)).ToDictionary(
      v => ZObjectDescriptor.ConvertValue<TA>(v.Split(inner).First())!,
      v => ZObjectDescriptor.ConvertValue<TB>(v.Split(inner).Last())!
    );

  public static string PackListAsId<T>(this List<T> list) where T : notnull => list.PackList("-");
  public static string PackList<T>(this List<T> list, string outer = ",", bool pad = true) where T : notnull =>
    (pad ? outer : "") + string.Join(outer, list) + (pad ? outer : "");

  public static List<T> UnpackList<T>(string val, string outer = ",", Func<string, T>? converter = null) where T : notnull => val.Split(outer)
    .Where(s => !string.IsNullOrWhiteSpace(s))
    .Select(v => converter == null ? ZObjectDescriptor.ConvertValue<T>(v)! : converter.Invoke(v))
    .ToList();
}

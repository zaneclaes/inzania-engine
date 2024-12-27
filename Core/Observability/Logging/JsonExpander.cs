#region

using System.Collections.Generic;
using System.Dynamic;
using System.Linq;

#endregion

namespace IZ.Core.Observability;

public static class JsonExpander {
  private static readonly string[] _typenameProps = {
    "__typename", "$type"
  };

  public static string? GetJsonObjectType(IDictionary<string, object?>? jsonDict) {
    if (jsonDict == null) return null;
    bool hasTypename = jsonDict.TryGetValue("__typename", out object? typenameObj);
    string? retVal = hasTypename ? typenameObj as string : null;

    bool hasTypeObj = jsonDict.TryGetValue("$type", out object? typeObj);
    retVal ??= hasTypeObj ? typeObj as string : null;
    return retVal;
  }

  public static string? GetJsonObjectType(this ExpandoObject obj) => GetJsonObjectType(new Dictionary<string, object?>(obj));

  private static object? ExpandObject(object? obj) {
    if (obj == null) return null;
    // Log.Information("OBJ {type}", obj.GetType());
    // if (obj is IEnumerable<string> strs) {
    //   return string.Join(',', strs);
    // }
    // if (obj is IEnumerable<ExpandoObject> enumExp) return enumExp.Select(ee => ee.ExpandoDictionary()).ToList();
    if (obj is ExpandoObject subObj) return subObj.ExpandoDictionary();
    return obj;
  }

  // Sanitizes obj into a readable dictionary by removing typenames
  public static IDictionary<string, object?> ExpandoDictionary(this ExpandoObject obj) {
    Dictionary<string, object?> dict = new Dictionary<string, object?>(obj);

    List<string>? keys = dict.Keys.Where(k => !_typenameProps.Contains(k)).ToList();

    return keys.ToDictionary(k => k, k => ExpandObject(dict[k]));
  }
}

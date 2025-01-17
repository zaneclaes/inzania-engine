#region

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

#endregion

namespace IZ.Core.Utils;

public static class TypeUtils {

  private static readonly ConcurrentDictionary<string, bool> _assignableTypes = new ConcurrentDictionary<string, bool>();
  public static Type? GetListType(this Type t) => t.IsListType() ? t.GetGenericArguments()[0] : null;

  public static bool IsEnumerableType(this Type t, Type? innerType = null) => t.IsGenericType && (innerType == null || t.GetGenericArguments()[0].IsAssignable(innerType));

  public static bool IsEnumerableType<T>(this Type t) => t.IsEnumerableType(typeof(T));

  public static bool IsListType(this Type t, Type? innerType = null) => t.IsGenericType && t.GetGenericTypeDefinition() == typeof(List<>) && (innerType == null || t.GetGenericArguments()[0].IsAssignable(innerType));

  public static bool IsListType<T>(this Type t) => t.IsListType(typeof(T));

  public static bool HasAssignableType(this Type? t, Type? toType) {
    if (t == toType) return true;
    if (t == null || toType == null) return false;
    var ut = Nullable.GetUnderlyingType(t);
    t = ut ?? t;
    string k = $"{t}_{toType}";
    return _assignableTypes.GetOrAdd(k, t2 => t.IsAssignable(toType) || t.IsEnumerableType(toType));
  }

  private static bool IsAssignable(this Type t, Type toType) {
    t = Nullable.GetUnderlyingType(t) != null ? Nullable.GetUnderlyingType(t)! : t;
    if (toType.IsInterface) return t.FindInterfaces((t2, o) => t2 == toType || t2.IsSubclassOf(toType), null).Length != 0;
    return t.IsSubclassOf(toType) || t == toType;
  }

  public static bool HasAssignableType<T>(this Type t) => t.HasAssignableType(typeof(T));
}

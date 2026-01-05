#region

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using IZ.Core.Api.Types;
using IZ.Core.Contexts;
using IZ.Core.Data;
using IZ.Core.Data.Attributes;
using IZ.Core.Utils;

#endregion

namespace IZ.Core.Api;

public static class ZApi {
  // private static readonly Dictionary<Type, Dictionary<Type, Dictionary<string, ZMethodDescriptor>>> ApiMethods =
  //   new Dictionary<Type, Dictionary<Type, Dictionary<string, ZMethodDescriptor>>>();
  //
  private static readonly Dictionary<Type, Dictionary<string, ZMethodDescriptor>> ApiMethodNames =
    new Dictionary<Type, Dictionary<string, ZMethodDescriptor>>();

  public static IZTypeMap TypeMap {
    get {
      if (_typeMap == null) {
        ZEnv.Log.Warning("[TYPES] generating type-map...");
        var tm = new ZApiTypeGenerator();
        _typeMap = tm;
        tm.InferSchema();
      }
      return _typeMap;
    }
    set => _typeMap = value;
  }
  private static IZTypeMap? _typeMap;


  public static Dictionary<string, ZMethodDescriptor> GetApiMethodNames<TRequest>() {
    if (ApiMethodNames.TryGetValue(typeof(TRequest), out var r)) return r;
    var ret = new Dictionary<string, ZMethodDescriptor>();
    if (TypeMap.ApiMethods.TryGetValue(typeof(TRequest), out var methods)) {
      foreach (var dict in methods.Values) {
        foreach (var name in dict.Keys) {
          ret[dict[name].FieldName] = dict[name];
        }
      }
    }
    ApiMethodNames[typeof(TRequest)] = ret;
    return ret;
  }

  public static ZTypeDescriptor LoadTypeDescriptor(Type t, bool isOptional = false) =>
    TypeMap.LoadTypeDescriptor(t, isOptional);

  public static ZObjectDescriptor LoadObjectDescriptor(Type t) => TypeMap.LoadObjectDescriptor(t);

  public static ZMethodDescriptor GetRequiredMethodByMethodName(ApiExecutionType opType, string methodName) {
    // EnsureSchema();
    Dictionary<string, ZMethodDescriptor>? names = GetMethodFieldNames(opType);
    return names.Values.FirstOrDefault(n => n.Name.Equals(methodName) || n.FieldName.Equals(methodName)) ?? throw new ArgumentException(
      $"{opType} does not contain {methodName} among ({string.Join(", ", names.Keys)})");
  }

  public static ZMethodDescriptor? GetMethod(ApiExecutionType opType, string methodName) {
    // EnsureSchema();
    Dictionary<string, ZMethodDescriptor>? names = GetMethodFieldNames(opType);
    return names.GetValueOrDefault(methodName);
  }

  private static Dictionary<string, ZMethodDescriptor> GetMethodFieldNames(ApiExecutionType opType) {
    // EnsureSchema();
    if (opType == ApiExecutionType.Query) return GetApiMethodNames<ZQueryBase>();
    if (opType == ApiExecutionType.Mutation) return GetApiMethodNames<ZMutationBase>();
    if (opType == ApiExecutionType.Subscription) return GetApiMethodNames<ZSubscriptionBase>();
    throw new ArgumentException($"{opType} not recognized");
  }

  public static Dictionary<Type, Dictionary<string, ZMethodDescriptor>> GetMethodImplementor(ApiExecutionType opType) {
    if (opType == ApiExecutionType.Query) return TypeMap.ApiMethods[typeof(ZQueryBase)];
    if (opType == ApiExecutionType.Mutation) return TypeMap.ApiMethods[typeof(ZMutationBase)];
    if (opType == ApiExecutionType.Subscription) return TypeMap.ApiMethods[typeof(ZSubscriptionBase)];
    throw new ArgumentException($"{opType} not recognized");
  }

  public static bool IsAssignableToBaseType<T>(this Type t) => t.IsAssignableToBaseType(typeof(T));
  public static bool IsAssignableToBaseType(this Type t, Type baseType) => t.HasAssignableType(baseType);
}

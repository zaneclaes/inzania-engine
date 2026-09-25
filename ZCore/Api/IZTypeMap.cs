using System;
using System.Collections;
using System.Collections.Generic;
using IZ.Core.Api.Types;
using IZ.Core.Contexts;
using IZ.Core.Utils;

namespace IZ.Core.Api;

/// <summary>
/// The API type map. <see cref="ApiTypes"/> and <see cref="ApiObjects"/> fill lazily on first use, and every
/// serializer call and request reaches them, so concurrent callers (parallel tests, server requests) load at
/// once. Every read and write of those two dictionaries holds the map's one lock (<see cref="SyncRoot"/>),
/// lookup and insert together, so each key gets exactly one descriptor. The lock is re-entrant, which the
/// recursive object → property → type loads need, and never contended on Unity's single-threaded WebGL player.
/// </summary>
public interface IZTypeMap {
  public Dictionary<Type, Dictionary<Type, Dictionary<string, ZMethodDescriptor>>> ApiMethods { get; }

  public Dictionary<string, ZTypeDescriptor> ApiTypes { get; }

  public Dictionary<string, ZObjectDescriptor> ApiObjects { get; }

  /// <summary>The lock guarding <see cref="ApiTypes"/> and <see cref="ApiObjects"/>; hold it to read either while requests run.</summary>
  public object SyncRoot => ApiObjects;

  public ZObjectDescriptor LoadObjectDescriptor(Type t) {
    var innerType = ZObjectDescriptor.StripOuterTypes(t);
    string key = innerType.Name;
    if (key.Contains("`") || key.Contains("[]")) throw new SystemException($"Invalid type {innerType} from {t}");
    lock (SyncRoot) {
      if (ApiObjects.TryGetValue(key, out var d)) return d;
      var descriptor = new ZObjectDescriptor(this, innerType);
      ApiObjects[key] = descriptor;
      return descriptor;
    }
  }

  public ZTypeDescriptor LoadTypeDescriptor(Type t, bool isOptional = false) {
    string key = $"{t}{(isOptional ? "?" : "!")}";
    lock (SyncRoot) {
      if (ApiTypes.TryGetValue(key, out var d)) return d;

      var innerType = ZMethodDescriptor.StripIgnoredOuterFunctionTypes(t);
      var ret = new ZTypeDescriptor();
      ZEnv.Log.Verbose("[TYPE] start {t}", innerType.Name);
      ret.OrigType = innerType;
      var nt1 = Nullable.GetUnderlyingType(innerType);
      if (nt1 != null) {
        innerType = nt1;
        ret.IsNullableOuter = true;
      } else if (isOptional) {
        ret.IsNullableOuter = true;
      }
      if (innerType.IsArray) {
        ZEnv.Log.Verbose("[TYPE] array {t}", innerType.Name);
        innerType = innerType.GetElementType()!;
        ret.IsList = true;
        ret.IsArray = true;
      } else if (innerType.HasAssignableType(typeof(IList))) {
        ZEnv.Log.Verbose("[TYPE] list {t}", innerType.Name);
        innerType = innerType.GenericTypeArguments[0];
        ret.IsList = true;
      } else if (innerType.HasAssignableType(typeof(IDictionary)) && innerType.GenericTypeArguments.Length == 2) {
        ret.DictionaryKeyType = innerType.GenericTypeArguments[0];
        innerType = innerType.GenericTypeArguments[1];
      }
      var nt2 = Nullable.GetUnderlyingType(innerType);
      if (ret.IsList && nt2 != null) {
        ZEnv.Log.Verbose("[TYPE] list-nullable {t}", innerType.Name);
        innerType = nt2;
        ret.IsNullableInner = true;
      }
      ret.ObjectDescriptor = LoadObjectDescriptor(innerType);
      // if (task) t = typeof(Task<>).MakeGenericType(t);
      ApiTypes[key] = ret;
      return ret;
    }
  }
}

#region

using System;
using System.Collections;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using IZ.Core.Api.Types;
using IZ.Core.Contexts;
using IZ.Core.Json;
using IZ.Core.Utils;

#endregion

namespace IZ.Core.Observability.Logging;

public static class ZLogging {
  public static IDictionary<string, object?> ExpandLogEntry(ExpandoObject obj) => obj.ExpandoDictionary();

  private static object TransformLoggableObject(object obj, int level = 0) {
    var t = obj.GetType();
    var desc = ZTypeDescriptor.FromType(t);
    if (desc.ObjectDescriptor.IsScalar) {
      if (desc.IsList) {
        return ((IEnumerable) obj).Cast<object>().ToList();
      }
      return obj;
    }
    if (level >= 5) {
      return $"{{ ...{desc.ObjectDescriptor.TypeName} }}"; // Special "overflow" indicator
    }
    if (desc.IsList) {
      return ((IEnumerable) obj).Cast<object?>().Select(o => o == null ? null : TransformLoggableObject(o, level + 1)).ToList();
    }
    if (!t.HasAssignableType(desc.ObjectDescriptor.ObjectType)) {
      return $"{{ {t}? }}";
    }
    Dictionary<string, object>? ret = new Dictionary<string, object>();
    foreach (var prop in desc.ObjectDescriptor.AllProperties) {
      if (prop.Observable == null) continue;
      object? v = null;
      try {
        v = prop.GetValue(obj);
      } catch (Exception e) {
        v = ZError.Guard(e);
      }
      if (v == null) continue;
      ret[prop.FieldName] = TransformLoggableObject(v, level + 1);
    }

    // var isList = t.IsEnumerableType<TObj>();
    // if (isList) return ((IEnumerable) mXml).Cast<object>().Select((o) => TransformLoggableObject<TObj>(o, level + 1)).ToList();
    // var props = t.GetProperties()
    //   .Where(p => p is {CanRead: true} && !p.GetCustomAttributes<LogIgnoreAttribute>().Any())
    //   .ToList();
    // ret.Add("_t", t.Name);

    // foreach (var p in props) {
    //   var val = p.GetValue(mXml);
    //   if (val == null) continue;
    //   if (val is TObj c) val = TransformLoggableObject<TObj>(c, level + 1);
    //
    //   var t2 = val.GetType();
    //   if (t2.IsEnumerableType<TObj>()) {
    //     var objs = ((IEnumerable) val).Cast<object>().Select((o) => TransformLoggableObject<TObj>(o, level + 1)).ToList();
    //     if (!objs.Any()) continue;
    //     val = objs;
    //   } else if (t2.GetListType() is { } listType) {
    //     if (!_scalars.Contains(listType) && !listType.IsEnum)
    //       ZEnv.Log.Information("[LIST] NO  {n} {t}", t2.Name, listType.Name);
    //   }
    //   ret.Add(p.Name, val);
    // }
    return level > 0 ? ret : ZJson.SerializeObject(ret, new ZJsonSerializationOpts {
      PrettyPrint = true
    });
  }

  public static object TransformObject<TObj>(object mXml) {
    try {
      return TransformLoggableObject(mXml);
    } catch (Exception e) {
      ZEnv.Log.Error(e, "[LOG] failed to transform {type} from {stack}", mXml.GetType().Name, new ZTrace(e.StackTrace));
      return mXml;
    }
  }

  private static List<Type> _scalars = new List<Type> {
    typeof(string),
    typeof(int),
    typeof(uint),
    typeof(float),
    typeof(decimal),
    typeof(byte),
    typeof(short),
    typeof(ushort),
    typeof(long),
    typeof(ulong)
  };

  public static string[] GetStackTrace(this Exception e) {
    return (e.StackTrace ?? "")
      .Split('\n')
      .Select(s => s.Trim())
      .Where(s => s.Length > 0)
      .ToArray();
  }

  public static TBuilder WithZData<TBuilder>(this TBuilder c) where TBuilder : LogBuilder {
    c
      // .Destructure.UsingAttributes()
      .TransformObjectWhere<object>(t => t.HasAssignableType<IGetLogged>(), TransformObject<IGetLogged>)
      // .Destructure.ToMaximumDepth(20)
      .TransformObject<ExpandoObject>(ExpandLogEntry)
      .TransformObject<Exception>((ex) => ZError.Guard(ex))
      // .Enrich.FromLogContext()
      ;
    return c;
  }
}

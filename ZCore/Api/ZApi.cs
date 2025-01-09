#region

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using IZ.Core.Api.Types;
using IZ.Core.Contexts;
using IZ.Core.Utils;

#endregion

namespace IZ.Core.Api;

public static class ZApi {
  private static readonly Dictionary<Type, Dictionary<Type, Dictionary<string, ZMethodDescriptor>>> ApiMethods =
    new Dictionary<Type, Dictionary<Type, Dictionary<string, ZMethodDescriptor>>>();

  private static readonly Dictionary<Type, Dictionary<string, ZMethodDescriptor>> ApiMethodNames =
    new Dictionary<Type, Dictionary<string, ZMethodDescriptor>>();

  public static Dictionary<string, ZMethodDescriptor> GetApiMethodNames<TRequest>() =>
    ApiMethodNames[typeof(TRequest)];

  private static bool IsExternal(Assembly asm) {
    var name = asm.ToString();
    return name.StartsWith("Microsoft.") || name.StartsWith("System") || name.StartsWith("Serilog") || name.StartsWith("netstandard")
           || name.StartsWith("HotChocolate")|| name.StartsWith("ChilliCream")|| name.StartsWith("MySql")|| name.StartsWith("GreenDonut")
           || name.StartsWith("Skia")|| name.StartsWith("Melanchall")|| name.StartsWith("MudBlazor")|| name.StartsWith("IdentityModel")
           || name.StartsWith("Pomelo")|| name.StartsWith("Anonymously")|| name.StartsWith("Datadog")|| name.StartsWith("WebOptimizer");
  }

  // Gets TOP LEVEL Api methods
  private static Dictionary<Type, Dictionary<string, ZMethodDescriptor>> CacheApiMethods<TRequest>() where TRequest : ZRequestBase {
    List<Assembly> assemblies = AppDomain.CurrentDomain.GetAssemblies().Where(a => !IsExternal(a)).ToList();
    /*new List<Assembly?> {
      typeof(TRequest).Assembly,
      Assembly.GetEntryAssembly(),
      Assembly.GetExecutingAssembly()
    }.Where(a => a != null).Distinct().Cast<Assembly>().ToList();

*/
    // ZEnv.Log.Information("[ASM] {asm}", string.Join("\n", assemblies.Select(a => a.ToString())));
    List<Type> queryTypes = assemblies.SelectMany(a => a.GetTypes())
      .Where(t => t.IsClass && t.IsSubclassOf(typeof(TRequest)))
      .Distinct()
      .ToList();
    Dictionary<Type, Dictionary<string, ZMethodDescriptor>> ret = new Dictionary<Type, Dictionary<string, ZMethodDescriptor>>();
    Dictionary<string, ZMethodDescriptor> methodNames = new Dictionary<string, ZMethodDescriptor>();

    if (!queryTypes.Any()) {
      ZEnv.Log.Warning("[API] no {type} types found in {assemblies}",
        typeof(TRequest), string.Join("\n", assemblies.Select(a => a.ToString())));
      return ret;
    }

    foreach (var t in queryTypes) {
      List<MethodInfo> methods = t.GetMethods()
        .Where(m => m.IsPublic && m.ReturnType.HasAssignableType(typeof(IZResult))).ToList();
      Dictionary<string, ZMethodDescriptor>? dict = methods.Select(m => new ZMethodDescriptor(m))
        .ToDictionary(m => m.FieldName, m => m);
      ret.Add(t, dict);
      foreach (string key in dict.Keys) {
        methodNames[key] = dict[key];
        dict[key].ExpandTypes(new List<ZTypeDescriptor>());
      }
    }
    ApiMethods[typeof(TRequest)] = ret;
    ApiMethodNames[typeof(TRequest)] = methodNames;
    ZEnv.Log.Debug("[SCHEMA] generated {type}: {@names}",
      typeof(TRequest), methodNames.Select(m => m.Key));
    return ret;
  }

  private static bool _hasSchema;

  internal static void EnsureSchema() {
    if (_hasSchema) return;
    ZEnv.Log.Information("[SCHEMA] loading...");
    CacheApiMethods<ZQueryBase>();
    ZEnv.Log.Information("[SCHEMA] query names: {@types}", ApiMethodNames[typeof(ZQueryBase)].Keys);

    CacheApiMethods<ZMutationBase>();
    ZEnv.Log.Debug("[SCHEMA] mutation names: {@types}", ApiMethodNames[typeof(ZMutationBase)].Keys);

    ZTypeDescriptor.ExpandTypeTree();
    ZEnv.Log.Debug("[SCHEMA] object types: {@types}", ZObjectDescriptor.ObjectTypes.Keys);
    ZEnv.Log.Debug("[SCHEMA] API types: {@types}", ZTypeDescriptor.ApiTypes.Values.Select(o => o.ToString()));

    _hasSchema = ZObjectDescriptor.ObjectTypes.Keys.Any();
    if (!_hasSchema) ZEnv.Log.Warning("[SCHEMA] failed {trace}", new ZTrace(new StackTrace().ToString()).ToString());
  }

  // public static Dictionary<string, TuneMethodDescriptor> GetObjectMethods(Type parentClass) {
  //   EnsureSchema();
  //   Dictionary<Type, Dictionary<string, TuneMethodDescriptor>>? res = ApiMethods.Values.FirstOrDefault(m => m.ContainsKey(parentClass));
  //   return res == null ? new Dictionary<string, TuneMethodDescriptor>() : res[parentClass];
  // }

  public static ZMethodDescriptor GetRequiredMethodByMethodName(ApiExecutionType opType, string methodName) {
    EnsureSchema();
    Dictionary<string, ZMethodDescriptor>? names = GetMethodFieldNames(opType);
    return names.Values.FirstOrDefault(n => n.OperationName.Equals(methodName) || n.FieldName.Equals(methodName)) ?? throw new ArgumentException(
      $"{opType} does not contain {methodName} among ({string.Join(", ", names.Keys)})");
  }

  public static ZMethodDescriptor? GetMethod(ApiExecutionType opType, string methodName) {
    EnsureSchema();
    Dictionary<string, ZMethodDescriptor>? names = GetMethodFieldNames(opType);
    return names.GetValueOrDefault(methodName);
  }

  private static Dictionary<string, ZMethodDescriptor> GetMethodFieldNames(ApiExecutionType opType) {
    EnsureSchema();
    if (opType == ApiExecutionType.Query) return ApiMethodNames[typeof(ZQueryBase)];
    if (opType == ApiExecutionType.Mutation) return ApiMethodNames[typeof(ZMutationBase)];
    throw new ArgumentException($"{opType} not recognized");
  }

  public static Dictionary<Type, Dictionary<string, ZMethodDescriptor>> GetMethodImplementor(ApiExecutionType opType) {
    if (opType == ApiExecutionType.Query) return ApiMethods[typeof(ZQueryBase)];
    if (opType == ApiExecutionType.Mutation) return ApiMethods[typeof(ZMutationBase)];
    throw new ArgumentException($"{opType} not recognized");
  }

  public static bool IsAssignableToBaseType<T>(this Type t) => t.IsAssignableToBaseType(typeof(T));
  public static bool IsAssignableToBaseType(this Type t, Type baseType) => t.HasAssignableType(baseType);

}

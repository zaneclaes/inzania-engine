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

public static class TuneApi {
  private static readonly Dictionary<Type, Dictionary<Type, Dictionary<string, TuneMethodDescriptor>>> ApiMethods =
    new Dictionary<Type, Dictionary<Type, Dictionary<string, TuneMethodDescriptor>>>();

  private static readonly Dictionary<Type, Dictionary<string, TuneMethodDescriptor>> ApiMethodNames =
    new Dictionary<Type, Dictionary<string, TuneMethodDescriptor>>();

  public static Dictionary<string, TuneMethodDescriptor> GetApiMethodNames<TRequest>() =>
    ApiMethodNames[typeof(TRequest)];

  private static bool IsExternal(Assembly asm) {
    var name = asm.ToString();
    return name.StartsWith("Microsoft.") || name.StartsWith("System") || name.StartsWith("Serilog") || name.StartsWith("netstandard")
           || name.StartsWith("HotChocolate")|| name.StartsWith("ChilliCream")|| name.StartsWith("MySql")|| name.StartsWith("GreenDonut")
           || name.StartsWith("Skia")|| name.StartsWith("Melanchall")|| name.StartsWith("MudBlazor")|| name.StartsWith("IdentityModel")
           || name.StartsWith("Pomelo")|| name.StartsWith("Anonymously")|| name.StartsWith("Datadog")|| name.StartsWith("WebOptimizer");
  }

  // Gets TOP LEVEL Api methods
  private static Dictionary<Type, Dictionary<string, TuneMethodDescriptor>> CacheApiMethods<TRequest>() where TRequest : TuneRequestBase {
    List<Assembly> assemblies = AppDomain.CurrentDomain.GetAssemblies().Where(a => !IsExternal(a)).ToList();
    /*new List<Assembly?> {
      typeof(TRequest).Assembly,
      Assembly.GetEntryAssembly(),
      Assembly.GetExecutingAssembly()
    }.Where(a => a != null).Distinct().Cast<Assembly>().ToList();

*/
    // IZEnv.Log.Information("[ASM] {asm}", string.Join("\n", assemblies.Select(a => a.ToString())));
    List<Type> queryTypes = assemblies.SelectMany(a => a.GetTypes())
      .Where(t => t.IsClass && t.IsSubclassOf(typeof(TRequest)))
      .Distinct()
      .ToList();
    Dictionary<Type, Dictionary<string, TuneMethodDescriptor>> ret = new Dictionary<Type, Dictionary<string, TuneMethodDescriptor>>();
    Dictionary<string, TuneMethodDescriptor> methodNames = new Dictionary<string, TuneMethodDescriptor>();

    if (!queryTypes.Any()) {
      IZEnv.Log.Warning("[API] no {type} types found in {@assemblies}",
        typeof(TRequest), assemblies.Select(a => a.ToString()));
      return ret;
    }

    foreach (var t in queryTypes) {
      List<MethodInfo> methods = t.GetMethods()
        .Where(m => m.IsPublic && m.ReturnType.HasAssignableType(typeof(ITuneResult))).ToList();
      Dictionary<string, TuneMethodDescriptor>? dict = methods.Select(m => new TuneMethodDescriptor(m))
        .ToDictionary(m => m.FieldName, m => m);
      ret.Add(t, dict);
      foreach (string key in dict.Keys) {
        methodNames[key] = dict[key];
        dict[key].ExpandTypes(new List<TuneTypeDescriptor>());
      }
    }
    ApiMethods[typeof(TRequest)] = ret;
    ApiMethodNames[typeof(TRequest)] = methodNames;
    IZEnv.Log.Debug("[SCHEMA] generated {type}: {@names}",
      typeof(TRequest), methodNames.Select(m => m.Key));
    return ret;
  }

  private static bool _hasSchema;

  internal static void EnsureSchema() {
    if (_hasSchema) return;
    IZEnv.Log.Debug("[SCHEMA] loading...");
    CacheApiMethods<TuneQueryBase>();
    IZEnv.Log.Debug("[SCHEMA] query names: {@types}", ApiMethodNames[typeof(TuneQueryBase)].Keys);

    CacheApiMethods<TuneMutationBase>();
    IZEnv.Log.Debug("[SCHEMA] mutation names: {@types}", ApiMethodNames[typeof(TuneMutationBase)].Keys);

    TuneTypeDescriptor.ExpandTypeTree();
    IZEnv.Log.Debug("[SCHEMA] object types: {@types}", TuneObjectDescriptor.ObjectTypes.Keys);
    IZEnv.Log.Debug("[SCHEMA] API types: {@types}", TuneTypeDescriptor.ApiTypes.Values.Select(o => o.ToString()));

    _hasSchema = TuneObjectDescriptor.ObjectTypes.Keys.Any();
    if (!_hasSchema) IZEnv.Log.Warning("[SCHEMA] failed {trace}", new TuneTrace(new StackTrace().ToString()).ToString());
  }

  public static Dictionary<string, TuneMethodDescriptor> GetObjectMethods(Type parentClass) {
    EnsureSchema();
    Dictionary<Type, Dictionary<string, TuneMethodDescriptor>>? res = ApiMethods.Values.FirstOrDefault(m => m.ContainsKey(parentClass));
    return res == null ? new Dictionary<string, TuneMethodDescriptor>() : res[parentClass];
  }

  public static TuneMethodDescriptor GetRequiredMethodByMethodName(ApiExecutionType opType, string methodName) {
    EnsureSchema();
    Dictionary<string, TuneMethodDescriptor>? names = GetMethodFieldNames(opType);
    return names.Values.FirstOrDefault(n => n.OperationName.Equals(methodName) || n.FieldName.Equals(methodName)) ?? throw new ArgumentException(
      $"{opType} does not contain {methodName} among ({string.Join(", ", names.Keys)})");
  }

  public static TuneMethodDescriptor? GetMethod(ApiExecutionType opType, string methodName) {
    EnsureSchema();
    Dictionary<string, TuneMethodDescriptor>? names = GetMethodFieldNames(opType);
    return names.GetValueOrDefault(methodName);
  }

  public static Dictionary<string, TuneMethodDescriptor> GetMethodFieldNames(ApiExecutionType opType) {
    EnsureSchema();
    if (opType == ApiExecutionType.Query) return ApiMethodNames[typeof(TuneQueryBase)];
    if (opType == ApiExecutionType.Mutation) return ApiMethodNames[typeof(TuneMutationBase)];
    throw new ArgumentException($"{opType} not recognized");
  }

  public static Dictionary<Type, Dictionary<string, TuneMethodDescriptor>> GetMethodImplementor(ApiExecutionType opType) {
    if (opType == ApiExecutionType.Query) return ApiMethods[typeof(TuneQueryBase)];
    if (opType == ApiExecutionType.Mutation) return ApiMethods[typeof(TuneMutationBase)];
    throw new ArgumentException($"{opType} not recognized");
  }

  public static bool IsAssignableToBaseType<T>(this Type t) => t.IsAssignableToBaseType(typeof(T));
  public static bool IsAssignableToBaseType(this Type t, Type baseType) => t.HasAssignableType(baseType);

}

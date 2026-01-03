#region

using System;
using System.Collections.Generic;
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
  private static readonly Dictionary<Type, Dictionary<Type, Dictionary<string, ZMethodDescriptor>>> ApiMethods =
    new Dictionary<Type, Dictionary<Type, Dictionary<string, ZMethodDescriptor>>>();

  private static readonly Dictionary<Type, Dictionary<string, ZMethodDescriptor>> ApiMethodNames =
    new Dictionary<Type, Dictionary<string, ZMethodDescriptor>>();
  private static List<Assembly>? _assemblies;
  private static List<Type>? _classes;

  private static bool _hasSchema;

  private static readonly SemaphoreSlim _startup = new SemaphoreSlim(1, 1);

  /*new List<Assembly?> {
    typeof(TRequest).Assembly,
    Assembly.GetEntryAssembly(),
    Assembly.GetExecutingAssembly()
  }.Where(a => a != null).Distinct().Cast<Assembly>().ToList();*/
  private static List<Assembly> Assemblies => _assemblies ??= AppDomain.CurrentDomain.GetAssemblies().Where(a => !IsExternal(a)).ToList();

  private static List<Type> Classes => _classes ??= Assemblies.SelectMany(a => a.GetTypes()).Distinct().ToList();

  public static Dictionary<string, ZMethodDescriptor> GetApiMethodNames<TRequest>() =>
    ApiMethodNames[typeof(TRequest)];

  private static bool IsExternal(Assembly asm) {
    string name = asm.ToString();
    return name.StartsWith("Microsoft.") || name.StartsWith("System") || name.StartsWith("Serilog") || name.StartsWith("netstandard")
           || name.StartsWith("HotChocolate") || name.StartsWith("ChilliCream") || name.StartsWith("MySql") || name.StartsWith("GreenDonut")
           || name.StartsWith("Skia") || name.StartsWith("Melanchall") || name.StartsWith("MudBlazor") || name.StartsWith("IdentityModel")
           || name.StartsWith("Pomelo") || name.StartsWith("Anonymously") || name.StartsWith("Datadog") || name.StartsWith("WebOptimizer");
  }

  private static List<Type> GetSubclasses(Type parentType) => Classes.Where(a => a.IsSubclassOf(parentType)).ToList();

  // Gets TOP LEVEL Api methods
  private static Dictionary<Type, Dictionary<string, ZMethodDescriptor>> CacheApiMethods<TRequest>()
    where TRequest : ZRequestBase {
    var queryTypes = GetSubclasses(typeof(TRequest)); // make sure *this* is cached elsewhere too
    var ret = new Dictionary<Type, Dictionary<string, ZMethodDescriptor>>(queryTypes.Count);
    var methodNames = new Dictionary<string, ZMethodDescriptor>(capacity: 256);
    var expandScratch = new HashSet<ZTypeDescriptor>(64);

    foreach (var t in queryTypes) {
      // Tighter flags reduces returned methods substantially.
      var methods = t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);

      // Build dict without LINQ
      var dict = new Dictionary<string, ZMethodDescriptor>(methods.Length);

      for (int i = 0; i < methods.Length; i++) {
        var m = methods[i];
        if (!m.IsPublic) continue;

        // Replace HasAssignableType(...) with direct IsAssignableFrom if possible.
        if (!typeof(IZResult).IsAssignableFrom(m.ReturnType)) continue;

        var d = new ZMethodDescriptor(m);
        dict[d.FieldName] = d;
        methodNames[d.FieldName] = d;

        expandScratch.Clear();
        d.ExpandTypes(expandScratch);
      }

      ret[t] = dict;
    }

    ApiMethods[typeof(TRequest)] = ret;
    ApiMethodNames[typeof(TRequest)] = methodNames;

    return ret;
  }


  // Any subclass of ApiObject that meets these criteria will be in the schema
  private static bool IsTypeExplicitlyIncluded(Type t) => t is {IsAbstract: false, IsGenericType: false, IsPublic: true} &&
                                                          t.GetCustomAttribute<ApiPacketAttribute>() != null;

  public static async ZTask WaitForSchema() {
    if (_hasSchema) return;
    await ZTask.WaitUntil(() => _hasSchema);
    ZEnv.Log.Information("[SCHEMA] schema loaded @{sec}", ZEnv.App.Uptime.TotalSeconds);
    return;
  }

  public static async ZTask EnsureSchemaAsync(int tries = 0) {
    await ZTask.Yield();
    await _startup.WaitAsync();
    try {
      if (_hasSchema) return;
      // ZEnv.Log.Debug("[SCHEMA] loading...");

      CacheApiMethods<ZQueryBase>();
      // ZEnv.Log.Debug("[SCHEMA] query names: {@types}", ApiMethodNames[typeof(ZQueryBase)].Keys);
      await ZTask.Yield();

      CacheApiMethods<ZMutationBase>();
      // ZEnv.Log.Debug("[SCHEMA] mutation names: {@types}", ApiMethodNames[typeof(ZMutationBase)].Keys);
      await ZTask.Yield();

      CacheApiMethods<ZSubscriptionBase>();
      // ZEnv.Log.Debug("[SCHEMA] subscription names: {@types}", ApiMethodNames[typeof(ZSubscriptionBase)].Keys);
      await ZTask.Yield();

      ZTypeDescriptor[] foundTypes = GetSubclasses(typeof(ApiObject))
        .Where(IsTypeExplicitlyIncluded)
        .Select(o => ZTypeDescriptor.FromType(o))
        .ToArray();
      await ZTask.Yield();

      // ZEnv.Log.Information("[SCHEMA] @{time} object types: {@types}", ZEnv.App.Uptime.TotalSeconds, ZObjectDescriptor.ObjectTypes.Keys);
      ZTypeDescriptor.ExpandTypeTree(foundTypes);
      await ZTask.Yield();
      // ZEnv.Log.Information("[SCHEMA] @{time} object types: {@types}", ZEnv.App.Uptime.TotalSeconds, ZObjectDescriptor.ObjectTypes.Keys);
      // ZEnv.Log.Debug("[SCHEMA] API types: {@types}", ZTypeDescriptor.ApiTypes.Values.Select(o => o.ToString()));

      _hasSchema = ZObjectDescriptor.ObjectTypes.Keys.Any();
      if (!_hasSchema) {
        ZEnv.Log.Warning("[SCHEMA] failed {trace}", new ZTrace());
        if (tries > 3) throw new SystemException("Failed to load schema");
        await EnsureSchemaAsync(tries + 1);
      }
      await ZTask.Yield();
    } finally {
      _startup.Release();
    }
  }

  public static ZMethodDescriptor GetRequiredMethodByMethodName(ApiExecutionType opType, string methodName) {
    // EnsureSchema();
    Dictionary<string, ZMethodDescriptor>? names = GetMethodFieldNames(opType);
    return names.Values.FirstOrDefault(n => n.OperationName.Equals(methodName) || n.FieldName.Equals(methodName)) ?? throw new ArgumentException(
      $"{opType} does not contain {methodName} among ({string.Join(", ", names.Keys)})");
  }

  public static ZMethodDescriptor? GetMethod(ApiExecutionType opType, string methodName) {
    // EnsureSchema();
    Dictionary<string, ZMethodDescriptor>? names = GetMethodFieldNames(opType);
    return names.GetValueOrDefault(methodName);
  }

  private static Dictionary<string, ZMethodDescriptor> GetMethodFieldNames(ApiExecutionType opType) {
    // EnsureSchema();
    if (opType == ApiExecutionType.Query) return ApiMethodNames[typeof(ZQueryBase)];
    if (opType == ApiExecutionType.Mutation) return ApiMethodNames[typeof(ZMutationBase)];
    if (opType == ApiExecutionType.Subscription) return ApiMethodNames[typeof(ZSubscriptionBase)];
    throw new ArgumentException($"{opType} not recognized");
  }

  public static Dictionary<Type, Dictionary<string, ZMethodDescriptor>> GetMethodImplementor(ApiExecutionType opType) {
    if (opType == ApiExecutionType.Query) return ApiMethods[typeof(ZQueryBase)];
    if (opType == ApiExecutionType.Mutation) return ApiMethods[typeof(ZMutationBase)];
    if (opType == ApiExecutionType.Subscription) return ApiMethods[typeof(ZSubscriptionBase)];
    throw new ArgumentException($"{opType} not recognized");
  }

  public static bool IsAssignableToBaseType<T>(this Type t) => t.IsAssignableToBaseType(typeof(T));
  public static bool IsAssignableToBaseType(this Type t, Type baseType) => t.HasAssignableType(baseType);
}

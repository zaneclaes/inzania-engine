using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using IZ.Core.Api.Types;
using IZ.Core.Contexts;
using IZ.Core.Data;
using IZ.Core.Data.Attributes;
using IZ.Core.Utils;

namespace IZ.Core.Api;

public class ZApiTypeGenerator : IZTypeMap {
  public Dictionary<Type, Dictionary<Type, Dictionary<string, ZMethodDescriptor>>> ApiMethods { get; }
    = new Dictionary<Type, Dictionary<Type, Dictionary<string, ZMethodDescriptor>>>();

  // public Dictionary<string, ZTypeDescriptor> ApiTypes { get; } = new Dictionary<string, ZTypeDescriptor>();

  public Dictionary<string, ZObjectDescriptor> ApiObjects { get; }
    = new Dictionary<string, ZObjectDescriptor>();

  private static List<Assembly>? _assemblies;
  private static List<Type>? _classes;

  private static List<Assembly> Assemblies => _assemblies ??= AppDomain.CurrentDomain.GetAssemblies().Where(a => !IsExternal(a)).ToList();

  private static List<Type> Classes => _classes ??= Assemblies.SelectMany(a => a.GetTypes()).Distinct().ToList();

  private static List<Type> GetSubclasses(Type parentType) => Classes.Where(a => a.IsSubclassOf(parentType)).ToList();

  private static bool IsExternal(Assembly asm) {
    string name = asm.ToString();
    return name.StartsWith("Microsoft.") || name.StartsWith("System") || name.StartsWith("Serilog") || name.StartsWith("netstandard")
           || name.StartsWith("HotChocolate") || name.StartsWith("ChilliCream") || name.StartsWith("MySql") || name.StartsWith("GreenDonut")
           || name.StartsWith("Skia") || name.StartsWith("Melanchall") || name.StartsWith("MudBlazor") || name.StartsWith("IdentityModel")
           || name.StartsWith("Pomelo") || name.StartsWith("Anonymously") || name.StartsWith("Datadog") || name.StartsWith("WebOptimizer");
  }

  // Any subclass of ApiObject that meets these criteria will be in the schema
  private static bool IsTypeExplicitlyIncluded(Type t) => t is {IsAbstract: false, IsGenericType: false, IsPublic: true} &&
                                                          t.GetCustomAttribute<ApiPacketAttribute>() != null;

  private static string GetMethodGroup(Type t) {
    if (t == typeof(ZQueryBase)) return "Query";
    if (t == typeof(ZMutationBase)) return "Mutation";
    if (t == typeof(ZSubscriptionBase)) return "Subscription";
    throw new ArgumentException($"Method {t}");
  }

  public async ZTask GenerateSourceFiles(string typeMapName, string dir, string ns) {
    ZApi.TypeMap = this;
    InferSchema();

    var usings = new HashSet<string>();
    var methodLines = new List<string>();
    foreach (var requestType in ApiMethods.Keys) {
      var mg = GetMethodGroup(requestType);
      var mgs = mg == "Query" ? "Queries" : $"{mg}s";
      var methodsDir = Path.Combine(dir, "Methods", mgs);
      var methodsNs = $"{ns}.Methods.{mgs}";
      usings.Add(methodsNs);
      if (Directory.Exists(methodsDir)) Directory.Delete(methodsDir, true);
      Directory.CreateDirectory(methodsDir);

      var typeMap = new List<string>();
      foreach (var qt in ApiMethods[requestType].Keys) {
        var methods = ApiMethods[requestType][qt];
        var methodMap = new List<string>();
        usings.Add(qt.Namespace!);
        foreach (var name in methods.Keys) {
          var method = methods[name];
          var cn = $"{method.Name}{mg}Descriptor";
          await File.WriteAllTextAsync(Path.Combine(methodsDir, $"{cn}.cs"), method.GetSource(cn, qt, methodsNs));
          methodMap.Add($"          [\"{name}\"] = new {cn}()");
        }
        typeMap.Add($"        [typeof({qt.Name})] = new Dictionary<string, ZMethodDescriptor>() {{\n" + string.Join(",\n", methodMap) + "\n        }");
      }
      methodLines.Add($"      [typeof({requestType.Name})] = new Dictionary<Type, Dictionary<string, ZMethodDescriptor>>() {{\n" + string.Join(",\n", typeMap) + "\n      }");
    }

    var objectsDir = Path.Combine(dir, "Objects");
    if (Directory.Exists(objectsDir)) Directory.Delete(objectsDir, true);
    Directory.CreateDirectory(objectsDir);
    usings.Add($"{ns}.Objects");

    var types = ZApi.TypeMap.ApiObjects.Values.ToList();
    var objLines = new List<string>();
    foreach (var objectType in types) {
      var cn = $"{objectType.TypeName}ObjectDescriptor";
      await File.WriteAllTextAsync(Path.Combine(objectsDir, $"{cn}.cs"), objectType.GetSource(cn, $"{ns}.Objects"));
      objLines.Add($"      [\"{objectType.TypeName}\"] = new {cn}()");
    }

    await File.WriteAllTextAsync(Path.Combine(dir, "TypeMap.cs"), $@"using System;
using System.Collections.Generic;
using IZ.Core.Api;
using IZ.Core.Api.Types;
using {string.Join(";\nusing ", usings)};

namespace Tuneality.Core.Types;

public class {typeMapName} : IZTypeMap {{
  public Dictionary<Type, Dictionary<Type, Dictionary<string, ZMethodDescriptor>>> ApiMethods {{ get; }}
  
  public Dictionary<string, ZObjectDescriptor> ApiObjects {{ get; }}

  public {typeMapName}() {{
    ApiMethods = new Dictionary<Type, Dictionary<Type, Dictionary<string, ZMethodDescriptor>>>() {{
{string.Join(",\n", methodLines)}
    }};

    ApiObjects = new Dictionary<string, ZObjectDescriptor>() {{
{string.Join(",\n", objLines)}
    }};
  }}
}}
");
  }

  public void InferSchema() {

    CacheApiMethods<ZQueryBase>();
    CacheApiMethods<ZMutationBase>();
    CacheApiMethods<ZSubscriptionBase>();

    ZTypeDescriptor[] foundTypes = GetSubclasses(typeof(ApiObject))
      .Where(IsTypeExplicitlyIncluded)
      .Select(o => ZTypeDescriptor.FromType(o))
      .ToArray();

    // ZEnv.Log.Information("[SCHEMA] @{time} object types: {@types}", ZEnv.App.Uptime.TotalSeconds, ZObjectDescriptor.ObjectTypes.Keys);
    ZTypeDescriptor.ExpandTypeTree(foundTypes);
    // ZEnv.Log.Information("[SCHEMA] @{time} object types: {@types}", ZEnv.App.Uptime.TotalSeconds, ZObjectDescriptor.ObjectTypes.Keys);
    // ZEnv.Log.Debug("[SCHEMA] API types: {@types}", ZTypeDescriptor.ApiTypes.Values.Select(o => o.ToString()));
    // ZObjectDescriptor.ObjectTypes.Keys.Any();
  }

  // Gets TOP LEVEL Api methods
  private Dictionary<Type, Dictionary<string, ZMethodDescriptor>> CacheApiMethods<TRequest>()
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
    // ApiMethodNames[typeof(TRequest)] = methodNames;

    return ret;
  }
}

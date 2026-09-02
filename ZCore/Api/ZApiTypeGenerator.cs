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

  public Dictionary<string, ZTypeDescriptor> ApiTypes { get; } = new Dictionary<string, ZTypeDescriptor>();

  public Dictionary<string, ZObjectDescriptor> ApiObjects { get; }
    = new Dictionary<string, ZObjectDescriptor>();

  private static List<Assembly>? _assemblies;
  private static List<Type>? _classes;

  /// <summary>
  /// The *loaded* assemblies, deliberately — not the reference closure. Crawling references drags in
  /// every third-party library the app merely links against (Stripe, Svg.Skia, WebDriver, …) and
  /// `ExpandTypeTree` then pulls their types into the schema: measured at +71 object descriptors over
  /// what the app actually publishes. A host that generates types is instead responsible for loading
  /// the projects that declare API surface before generating — and, because a bare `typeof(X)` is
  /// side-effect-free and the Release JIT drops it, it must do so in a way that survives optimisation
  /// (use `typeof(X).Assembly` and consume the result). See ChordzyCli's `generate-types`.
  /// </summary>
  private static List<Assembly> Assemblies => _assemblies ??=
    AppDomain.CurrentDomain.GetAssemblies().Where(a => !IsExternal(a)).ToList();

  /// <summary>The assemblies the schema scan will read; useful when a host generates an empty schema.</summary>
  public static List<string> ScannedAssemblyNames() => Assemblies.Select(a => a.GetName().Name ?? "?").OrderBy(n => n).ToList();

  /// <summary>Forgets the cached assembly/type scan, so a later generation re-discovers everything.</summary>
  public static void ResetAssemblyCache() {
    _assemblies = null;
    _classes = null;
  }

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

  /// <summary>
  /// Regenerates the descriptor sources for <paramref name="dir" /> so that what is on disk is exactly
  /// what the loaded assemblies describe: every descriptor the emitted type map references exists, and
  /// nothing else does.
  ///
  /// Files are written first and stale ones pruned afterwards, never the reverse. The previous order
  /// (`Directory.Delete(recursive)` then `CreateDirectory` then write) could lose the first few files
  /// of a directory — the writes landed in the unlinked inode — leaving a type map that referenced
  /// descriptors with no source, which only surfaced as a compile error later. It also meant an
  /// exception part-way through generation left the tree gutted. <see cref="VerifyGenerated" /> then
  /// fails loudly rather than letting a missing descriptor through.
  /// </summary>
  public async ZTask GenerateSourceFiles(string typeMapName, string dir, string ns) {
    ZApi.TypeMap = this;
    InferSchema();

    // Every file this run is responsible for, per directory: anything else in those directories is
    // stale (a renamed or deleted API class) and is removed once all writes have succeeded.
    var written = new Dictionary<string, HashSet<string>>();
    var expected = new List<string>();

    async ZTask Emit(string targetDir, string className, string source) {
      Directory.CreateDirectory(targetDir);
      string path = Path.Combine(targetDir, $"{className}.cs");
      await File.WriteAllTextAsync(path, source);
      if (!written.TryGetValue(targetDir, out var files)) written[targetDir] = files = new HashSet<string>();
      files.Add(Path.GetFileName(path));
      expected.Add(path);
    }

    var usings = new HashSet<string>();
    var methodLines = new List<string>();
    foreach (var requestType in ApiMethods.Keys) {
      var mg = GetMethodGroup(requestType);
      var mgs = mg == "Query" ? "Queries" : $"{mg}s";
      var methodsDir = Path.Combine(dir, "Methods", mgs);
      var methodsNs = $"{ns}.Methods.{mgs}";
      // Only if the group actually emits a descriptor: a `using` of a namespace no file declares is
      // CS0234, so a host with (say) no subscriptions could not compile its own generated type map.
      if (ApiMethods[requestType].Values.Any(m => m.Count > 0)) usings.Add(methodsNs);
      // Claim the directory even when this group has no methods, so emptying a group prunes it.
      if (!written.ContainsKey(methodsDir)) written[methodsDir] = new HashSet<string>();

      var typeMap = new List<string>();
      foreach (var qt in ApiMethods[requestType].Keys) {
        var methods = ApiMethods[requestType][qt];
        var methodMap = new List<string>();
        usings.Add(qt.Namespace!);
        foreach (var name in methods.Keys) {
          var method = methods[name];
          var cn = $"{method.Name}{mg}Descriptor";
          await Emit(methodsDir, cn, method.GetSource(this, cn, qt, methodsNs));
          methodMap.Add($"          [\"{name}\"] = new {cn}(this)");
        }
        typeMap.Add($"        [typeof({qt.Name})] = new Dictionary<string, ZMethodDescriptor>() {{\n" + string.Join(",\n", methodMap) + "\n        }");
      }
      methodLines.Add($"      [typeof({requestType.Name})] = new Dictionary<Type, Dictionary<string, ZMethodDescriptor>>() {{\n" + string.Join(",\n", typeMap) + "\n      }");
    }

    var objectsDir = Path.Combine(dir, "Objects");
    usings.Add($"{ns}.Objects");
    if (!written.ContainsKey(objectsDir)) written[objectsDir] = new HashSet<string>();

    var types = ZApi.TypeMap.ApiObjects.Values.ToList();
    var objLines = new List<string>();
    foreach (var objectType in types) {
      var cn = $"{objectType.TypeName}ObjectDescriptor";
      await Emit(objectsDir, cn, objectType.GetSource(this, cn, $"{ns}.Objects"));
      objLines.Add($"      [\"{objectType.TypeName}\"] = new {cn}(this)");
    }

    PruneStale(written);
    VerifyGenerated(expected);

    await File.WriteAllTextAsync(Path.Combine(dir, $"{typeMapName}.cs"), $@"using System;
using System.Collections.Generic;
using IZ.Core.Api;
using IZ.Core.Api.Types;
using {string.Join(";\nusing ", usings)};

namespace Tuneality.Core.Types;

public class {typeMapName} : IZTypeMap {{
  public Dictionary<Type, Dictionary<Type, Dictionary<string, ZMethodDescriptor>>> ApiMethods {{ get; }}

  public Dictionary<string, ZTypeDescriptor> ApiTypes {{ get; }} = new Dictionary<string, ZTypeDescriptor>();
  
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

  /// <summary>
  /// Deletes descriptor sources this run did not write — the counterpart of an API class being renamed
  /// or removed. Only `*.cs` directly inside the generated directories is considered, so nothing a
  /// human put nearby is at risk.
  /// </summary>
  private static void PruneStale(Dictionary<string, HashSet<string>> written) {
    foreach (var (targetDir, keep) in written) {
      if (!Directory.Exists(targetDir)) continue;
      foreach (string path in Directory.GetFiles(targetDir, "*.cs", SearchOption.TopDirectoryOnly)) {
        if (keep.Contains(Path.GetFileName(path))) continue;
        ZEnv.Log.Information("[TYPES] pruning stale descriptor {path}", path);
        File.Delete(path);
      }
    }
  }

  /// <summary>
  /// Re-reads the tree and throws if any descriptor the type map will reference is missing. A silently
  /// absent descriptor produces a type map that does not compile, which is a much worse signal than a
  /// generation failure — so generation fails instead.
  /// </summary>
  private static void VerifyGenerated(List<string> expected) {
    var missing = expected.Where(p => !File.Exists(p)).ToList();
    if (missing.Count <= 0) return;
    throw new SystemException(
      $"[TYPES] {missing.Count} descriptor(s) were not written: {string.Join(", ", missing.Select(Path.GetFileName))}. " +
      "The generated type map would reference sources that do not exist.");
  }

  public void InferSchema() {
    // Start from empty. Emitting a descriptor's source resolves the types it mentions through
    // ZApi.TypeMap (which is this instance), so generation itself adds entries — `Object`,
    // `CancellationToken`, `IZContext`. Left in place, the next InferSchema would treat those as part
    // of the schema and emit descriptors for them, so the second generation of a process produced a
    // strictly different — and wrong — answer from the first.
    ApiMethods.Clear();
    ApiTypes.Clear();
    ApiObjects.Clear();

    CacheApiMethods<ZQueryBase>();
    CacheApiMethods<ZMutationBase>();
    CacheApiMethods<ZSubscriptionBase>();

    ZTypeDescriptor[] foundTypes = GetSubclasses(typeof(ApiObject))
      .Where(IsTypeExplicitlyIncluded)
      .Select(o => ((IZTypeMap)this).LoadTypeDescriptor(o))
      .ToArray();

    // ZEnv.Log.Information("[SCHEMA] @{time} object types: {@types}", ZEnv.App.Uptime.TotalSeconds, ZObjectDescriptor.ObjectTypes.Keys);
    ZTypeDescriptor.ExpandTypeTree(this, foundTypes);
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

        var d = new ZMethodDescriptor(this, m);
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

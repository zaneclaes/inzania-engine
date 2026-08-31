#region

using System.Reflection;
using IZ.Core.Api;

#endregion

namespace ZTests;

/// <summary>
/// A <see cref="ZQueryBase" />/<see cref="ZMutationBase" />/<see cref="ZSubscriptionBase" /> subclass
/// only becomes part of a project's API surface once an aggregate *registration* class exposes it as
/// a public property (Chordzy: `TuneQuery`/`TuneMutation`/`TuneSubscription` off `TuneRequest`).
/// Forgetting that one line leaves a class that compiles, generates descriptors and looks finished
/// but is unreachable through the request tree.
///
/// The registration class name differs per project, so nothing here is hard-coded: a registration
/// class is any loaded, non-API class that declares a public property typed as an API class. Call
/// <see cref="FindUnregistered" /> from a test in the consuming project — reflection only sees
/// *loaded* assemblies, so the test must touch the project's request root first (referencing the
/// type is enough).
/// </summary>
public static class ZApiRegistration {
  private static readonly Type[] ApiBases = { typeof(ZQueryBase), typeof(ZMutationBase), typeof(ZSubscriptionBase) };

  private static bool IsExternal(Assembly asm) {
    string name = asm.GetName().Name ?? "";
    return name.StartsWith("Microsoft.") || name.StartsWith("System") || name.StartsWith("netstandard") ||
           name.StartsWith("Serilog") || name.StartsWith("HotChocolate") || name.StartsWith("ChilliCream") ||
           name.StartsWith("GreenDonut") || name.StartsWith("MySql") || name.StartsWith("Pomelo") ||
           name.StartsWith("Microting") || name.StartsWith("Skia") || name.StartsWith("Melanchall") ||
           name.StartsWith("MudBlazor") || name.StartsWith("IdentityModel") || name.StartsWith("Datadog") ||
           name.StartsWith("WebOptimizer") || name.StartsWith("xunit") || name.StartsWith("FluentAssertions") ||
           name.StartsWith("Anonymously");
  }

  private static List<Type> LoadedTypes() {
    var types = new List<Type>();
    foreach (var asm in AppDomain.CurrentDomain.GetAssemblies()) {
      if (IsExternal(asm)) continue;
      try { types.AddRange(asm.GetTypes()); } catch (ReflectionTypeLoadException e) {
        types.AddRange(e.Types.Where(t => t != null)!);
      }
    }
    return types.Distinct().ToList();
  }

  /// <summary>Concrete API classes that declare at least one endpoint (an `IZResult&lt;&gt;` method).</summary>
  public static List<Type> ApiClasses(IEnumerable<Type>? types = null) =>
    (types ?? LoadedTypes())
    .Where(t => t is { IsAbstract: false, IsGenericTypeDefinition: false } &&
                ApiBases.Any(b => t.IsSubclassOf(b)) &&
                t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                  .Any(m => m.ReturnType.IsGenericType &&
                            typeof(IZResult).IsAssignableFrom(m.ReturnType)))
    .OrderBy(t => t.FullName)
    .ToList();

  /// <summary>
  /// API classes that no registration class exposes. An empty list is the healthy state; each entry
  /// is a class whose endpoints are unreachable through the project's request tree.
  /// </summary>
  public static List<Type> FindUnregistered() {
    var types = LoadedTypes();
    var api = ApiClasses(types);
    if (api.Count <= 0) return api;

    var apiSet = new HashSet<Type>(api);
    var registered = new HashSet<Type>();
    foreach (var t in types) {
      if (t.IsAbstract || ApiBases.Any(b => t.IsSubclassOf(b))) continue; // an API class holding another is not a registry
      foreach (var p in t.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)) {
        if (apiSet.Contains(p.PropertyType)) registered.Add(p.PropertyType);
      }
    }
    return api.Where(t => !registered.Contains(t)).ToList();
  }
}

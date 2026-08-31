#region

using System.Reflection;
using IZ.Core.Api;

#endregion

namespace ZTests;

/// <summary>
/// Reflection scan over the loaded assemblies for the project's API surface — every concrete
/// <see cref="ZQueryBase" />/<see cref="ZMutationBase" />/<see cref="ZSubscriptionBase" /> subclass
/// that declares at least one endpoint (an `IZResult&lt;&gt;` method). This is the same population
/// `ZApiTypeGenerator` turns into the type map and `Context.BeginRequest&lt;TReq&gt;()` constructs
/// reflectively, so tests built on it (<see cref="ZApiAuthorization" />, type-map coverage) audit
/// exactly what is callable. Reflection only sees *loaded* assemblies, so a test must touch a type
/// from each API project first (constructing any one API class is enough).
/// </summary>
public static class ZApiSurface {
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
}

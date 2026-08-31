#region

using System.Reflection;
using IZ.Core.Api;
using IZ.Core.Data.Attributes;

#endregion

namespace ZTests;

/// <summary>
/// Authorization on an endpoint is opt-in: a public `IZResult&lt;&gt;` method on a
/// <see cref="ZQueryBase" />/<see cref="ZMutationBase" />/<see cref="ZSubscriptionBase" /> class is
/// served to ANONYMOUS callers unless it declares <see cref="ApiAuthorizeAttribute" /> (the schema
/// layer only emits an authorization directive for methods that carry it). A missing attribute is
/// invisible — the endpoint compiles, generates and serves — so the set of deliberately-public
/// endpoints must be pinned explicitly.
///
/// Call <see cref="FindAnonymousEndpoints" /> from a test in the consuming project and assert the
/// result equals the project's known allowlist (login/signUp-style flows); any new entry is then a
/// conscious decision, not an accident. The edit-time counterpart is the `ApiAuthGuard` hook next
/// to `ApiRegistrationGuard`, which additionally sees method BODIES (identity dereferences, data
/// writes) that reflection cannot. Reflection only sees *loaded* assemblies, so the test must touch
/// the project's request root first (referencing the type is enough).
/// </summary>
public static class ZApiAuthorization {
  /// <summary>Every endpoint (public `IZResult&lt;&gt;` method) declared by the loaded API classes.</summary>
  public static List<MethodInfo> Endpoints() =>
    ZApiRegistration.ApiClasses()
      .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
      .Where(m => m.ReturnType.IsGenericType && typeof(IZResult).IsAssignableFrom(m.ReturnType))
      .OrderBy(m => $"{m.DeclaringType?.Name}.{m.Name}")
      .ToList();

  /// <summary>
  /// Endpoints with no <see cref="ApiAuthorizeAttribute" />, as `DeclaringType.Method` names. Each
  /// entry is served without authentication; the healthy state is a short, explicitly-audited list.
  /// </summary>
  public static List<string> FindAnonymousEndpoints() =>
    Endpoints()
      .Where(m => m.GetCustomAttribute<ApiAuthorizeAttribute>() == null)
      .Select(m => $"{m.DeclaringType?.Name}.{m.Name}")
      .ToList();
}

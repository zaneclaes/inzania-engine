#region

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using IZ.Core;
using IZ.Core.Contexts;
using IZ.Core.Observability.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;

#endregion

namespace IZ.Server;

/// <summary>
/// The start-up configuration check <see cref="ZHostApp{TDb}" /> runs before it serves.
///
/// <para>An environment variable named <c>Section__Key</c> overrides the configuration key
/// <c>Section:Key</c>. When that key is declared in no committed <c>appsettings*.json</c>, nothing reads
/// it: the deployment believes it set a secret while the process runs the committed value. That is how
/// production ran the committed SendGrid and Stripe keys under <c>ApiCredentials__…</c> names that bound
/// nothing (Chordzy <c>Docs/Plans/SECURITY-2026-09-26.md</c> F13/R9). A key the code reads only from the
/// environment is declared in <c>appsettings.json</c> with an empty value, which also documents it, so
/// there is no allowlist here.</para>
/// </summary>
public static class ConfigCheck {
  /// <summary>
  /// The names in <paramref name="environment" /> that address a configuration key no committed JSON
  /// file declares. Judged: names containing <c>__</c> whose every segment is non-empty (so an OS name
  /// such as macOS's <c>__CF_USER_TEXT_ENCODING</c>, which cannot address a key, is not). Not judged:
  /// names without <c>__</c> (read by name through <c>Environment.GetEnvironmentVariable</c>) and names
  /// under <c>ASPNETCORE_</c>/<c>DOTNET_</c>, which belong to the framework. Sorted, ordinal.
  /// </summary>
  public static IReadOnlyList<string> UnboundEnvironmentNames(IConfigurationRoot config, IDictionary environment) {
    // The committed files (`AddJsonFile`) and, for tests, the same JSON handed over as a stream.
    var json = config.Providers.Where(p => p is JsonConfigurationProvider or JsonStreamConfigurationProvider).ToList();
    var ret = new List<string>();
    foreach (DictionaryEntry entry in environment) {
      if (entry.Key is not string name || !IsJudged(name)) continue;
      string key = name.Replace("__", ConfigurationPath.KeyDelimiter);
      if (!json.Any(p => p.TryGet(key, out _))) ret.Add(name);
    }
    ret.Sort(StringComparer.Ordinal);
    return ret;
  }

  private static bool IsJudged(string name) {
    if (!name.Contains("__", StringComparison.Ordinal)) return false;
    if (name.StartsWith("ASPNETCORE_", StringComparison.OrdinalIgnoreCase) ||
        name.StartsWith("DOTNET_", StringComparison.OrdinalIgnoreCase)) return false;
    return name.Split("__").All(s => s.Length > 0);
  }

  /// <summary>The problem line for one unbound name. The name is the whole message: never a value.</summary>
  public static string Unbound(string name) => $"env {name} binds nothing";

  /// <summary>
  /// At <see cref="ZEnvironment.Development" /> and below (local runs, test hosts) throw with every
  /// problem listed. On a deployed environment log each once at <c>Error</c> as <c>[CONFIG] …</c> and
  /// call <paramref name="count" /> for the metric: a dead name leaves the committed value in force, and a
  /// crash loop over a fault only the operator can fix would take the site down.
  /// </summary>
  public static void Apply(IReadOnlyList<string> problems, ZEnvironment env, IZLogger log, Action<string> count) {
    if (problems.Count == 0) return;
    if (env <= ZEnvironment.Development)
      throw new InvalidOperationException("[CONFIG] " + string.Join("; ", problems));
    foreach (string problem in problems) {
      log.Error("[CONFIG] {problem}", problem);
      count(problem);
    }
  }
}

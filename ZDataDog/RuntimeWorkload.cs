#region

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using IZ.Core;
using IZ.Core.Observability.Logging;

#endregion

namespace IZ.Observability.DataDog;

/// <summary>
/// Bounded workload identity for runtime metrics: pod, ReplicaSet, deployment, release.
/// Request and user identifiers are never tags.
/// </summary>
public static class RuntimeWorkload {
  public static readonly string[] AllowedTags = {
    "pod_name", "kube_replica_set", "kube_deployment", "version"
  };

  public static IReadOnlyDictionary<string, string> Current { get; private set; } =
    new Dictionary<string, string>();

  public static IReadOnlyDictionary<string, string> Resolve(string? pod, string? releaseSha, string? releaseVersion) {
    var tags = new Dictionary<string, string>();
    if (TryPod(pod, out string name, out string replicaSet, out string deployment)) {
      tags["pod_name"] = name;
      tags["kube_replica_set"] = replicaSet;
      tags["kube_deployment"] = deployment;
    }
    string? version = ReleaseIdentity(releaseSha, releaseVersion);
    if (version != null) tags["version"] = version;
    return tags;
  }

  public static IReadOnlyDictionary<string, string> Capture() {
    string? pod = Environment.GetEnvironmentVariable("POD_NAME");
    if (string.IsNullOrWhiteSpace(pod)) pod = Environment.GetEnvironmentVariable("HOSTNAME");
    string? sha = First(
      Environment.GetEnvironmentVariable("CHORDZY_RELEASE_SHA"),
      Environment.GetEnvironmentVariable("TUNE_BUILD_SHA"),
      ReadBuildSha());
    if (!IsSha(sha) && !IsVersion(sha)) {
      string? dd = Environment.GetEnvironmentVariable("DD_VERSION");
      if (IsSha(dd) || IsVersion(dd)) sha = dd;
    }
    string? build = Environment.GetEnvironmentVariable("TUNE_BUILD_NUMBER");
    var tags = Resolve(pod, sha, build);
    Current = tags;
    return tags;
  }

  private static string? ReleaseIdentity(string? sha, string? version) {
    if (IsSha(sha) && !RuntimeExceptionReport.IdentifiesUserOrRequest(sha)) return sha!.Trim();
    if (IsVersion(version) && !RuntimeExceptionReport.IdentifiesUserOrRequest(version)) return version!.Trim();
    if (IsVersion(sha) && !RuntimeExceptionReport.IdentifiesUserOrRequest(sha)) return sha!.Trim();
    return null;
  }

  private static bool TryPod(string? pod, out string name, out string replicaSet, out string deployment) {
    name = "";
    replicaSet = "";
    deployment = "";
    if (string.IsNullOrWhiteSpace(pod)) return false;
    string trimmed = pod.Trim();
    if (RuntimeExceptionReport.IdentifiesUserOrRequest(trimmed)) return false;
    Match match = PodName.Match(trimmed);
    if (!match.Success) return false;
    deployment = match.Groups["deploy"].Value;
    if (deployment.Length == 0 || RuntimeExceptionReport.IdentifiesUserOrRequest(deployment)) return false;
    replicaSet = deployment + "-" + match.Groups["hash"].Value;
    name = trimmed;
    return true;
  }

  private static bool IsSha(string? value) =>
    !string.IsNullOrWhiteSpace(value) && Sha.IsMatch(value.Trim());

  private static bool IsVersion(string? value) =>
    !string.IsNullOrWhiteSpace(value) && VersionToken.IsMatch(value.Trim());

  private static string? First(params string?[] values) {
    foreach (string? value in values)
      if (!string.IsNullOrWhiteSpace(value)) return value;
    return null;
  }

  private static string? ReadBuildSha() {
    var paths = new List<string> { "/chordzy/user/meta/build-sha" };
    try {
      string? dir = ZEnv.App?.Storage?.UserDir;
      if (!string.IsNullOrEmpty(dir)) paths.Insert(0, Path.Combine(dir, "meta", "build-sha"));
    } catch (Exception) {
      // Storage is unset until the host finishes building settings.
    }
    paths.Add(Path.Combine(Directory.GetCurrentDirectory(), "meta", "build-sha"));
    foreach (string path in paths) {
      try {
        if (!File.Exists(path)) continue;
        string text = File.ReadAllText(path).Trim();
        if (text.Length > 0 && !text.Equals("unknown", StringComparison.OrdinalIgnoreCase)) return text;
      } catch (Exception) {
        // An unreadable build file is omitted, not a startup failure.
      }
    }
    return null;
  }

  private static readonly Regex PodName = new Regex(
    @"^(?<deploy>[a-z0-9]+(?:-[a-z0-9]+)*)-(?<hash>[a-z0-9]{8,10})-(?<id>[a-z0-9]{5})$",
    RegexOptions.Compiled);

  private static readonly Regex Sha = new Regex(@"^[0-9a-fA-F]{7,40}$", RegexOptions.Compiled);

  private static readonly Regex VersionToken = new Regex(
    @"^[0-9]{1,8}(\.[0-9]{1,8}){0,3}$", RegexOptions.Compiled);
}

using System;
using System.Linq;
using IZ.Core.Data;

namespace IZ.Core.Utils;

public class SemVersion : TransientObject {
  public string Full { get; }

  public ushort Major { get; }

  public ushort Minor { get; }

  public ushort Patch { get; }

  public string? Prerelease { get; }

  public string? Metadata { get; }

  public SemVersion(ushort major, ushort minor, ushort patch, string? prerelease = null, string? metadata = null) {
    Major = major;
    Minor = minor;
    Patch = patch;
    Prerelease = prerelease;
    Metadata = metadata;

    var full = $"{Major}.{Minor}.{Patch}";
    if (!string.IsNullOrEmpty(Prerelease)) full += $"-{Prerelease}";
    if (!string.IsNullOrEmpty(Metadata)) full += $"+{Metadata}";
    Full = full;
  }

  public override string ToString() => Full;

  public static SemVersion Parse(string version) {
    string? prerelease = null, metadata = null;

    var parts = version.Split('.').ToList();
    if (parts.Count < 3) throw new FormatException($"SemVersion must have 3 parts: {version}");
    if (!ushort.TryParse(parts[0], out var major)) throw new FormatException($"Major is not a number: {version}");
    if (!ushort.TryParse(parts[1], out var minor)) throw new FormatException($"Minor is not a number: {version}");

    parts.RemoveRange(0, 2);
    var tail = string.Join('.', parts);

    var metaIdx = tail.IndexOf('+');
    if (metaIdx >= 0) {
      metadata = tail.Substring(metaIdx + 1);
      if (string.IsNullOrWhiteSpace(metadata)) metadata = null;
      tail = tail.Substring(0, metaIdx);
    }
    var preIdx = tail.IndexOf('-');
    if (preIdx >= 0) {
      prerelease = tail.Substring(preIdx + 1);
      if (string.IsNullOrWhiteSpace(prerelease)) prerelease = null;
      tail = tail.Substring(0, preIdx);
    }

    if (tail.Length == 0 || !ushort.TryParse(tail, out var patch))
      throw new FormatException($"Patch is not a non-negative integer: {version}");

    return new SemVersion(major, minor, patch, prerelease, metadata);
  }
}

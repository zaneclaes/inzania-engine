#region

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using IZ.Core;
using IZ.Core.Contexts;

#endregion

namespace IZ.Core.Utils;

/// <summary>
/// The inverse of <see cref="ZEnv.SerializeZEnum{T}" />, and the only place an enum is parsed off the
/// wire. Two separate bugs live here if it is done by hand:
/// <list type="number">
///   <item>
///     Reconstructing the C# name from the SCREAMING_SNAKE one by string surgery is not the inverse of
///     anything — it is a second, independently-drifting spelling of the naming policy. It also could
///     not answer `DOWNLOAD_MIDI` at all in the one place GraphQL hands the schema name straight to
///     <c>Enum.Parse</c>, so every multi-word enum was unusable as a query/mutation argument.
///   </item>
///   <item>
///     Throwing on a name the enum does not have makes ADDING an enum value a breaking change for
///     every client already in the wild — an app in the stores, a cached WebGL build. One new value in
///     one field killed the whole `webPage` query rather than the one button it described.
///   </item>
/// </list>
/// So: the map is built FROM <see cref="ZEnv.SerializeZEnum{T}" /> (it cannot drift), and an unknown
/// value degrades to the type's <see cref="Fallback" /> — `Unknown`/`None`/0 — with a warning, instead
/// of throwing. Serialization stays strict; only inbound parsing is forgiving.
/// </summary>
public static class ZEnums {
  private const string UnknownName = "Unknown";
  private const string NoneName = "None";

  private static readonly ConcurrentDictionary<Type, Dictionary<string, object>> _wireNames =
    new ConcurrentDictionary<Type, Dictionary<string, object>>();

  private static readonly ConcurrentDictionary<Type, object> _fallbacks = new ConcurrentDictionary<Type, object>();

  private static readonly ConcurrentDictionary<string, bool> _warned = new ConcurrentDictionary<string, bool>();

  /// <summary>Every accepted spelling of every member: the wire name, and the raw C# name.</summary>
  public static Dictionary<string, object> WireNames(Type enumType) =>
    _wireNames.GetOrAdd(enumType, BuildWireNames);

  private static Dictionary<string, object> BuildWireNames(Type enumType) {
    if (!enumType.IsEnum) throw new ArgumentException($"{enumType} is not an enum");
    // OrdinalIgnoreCase preserves the tolerance callers had from `Enum.Parse(t, val, true)`.
    var map = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
    foreach (string name in Enum.GetNames(enumType)) {
      object val = Enum.Parse(enumType, name);
      map[name.ToSnakeCase().ToUpperInvariant()] = val; // DOWNLOAD_MIDI — what crosses the wire
      map[name] = val; // DownloadMidi — the C# spelling, still accepted
    }
    return map;
  }

  /// <summary>
  /// What an unrecognized value becomes: the `Unknown` member if the enum declares one, else `None`,
  /// else whatever 0 maps to (the engine's enums put `Unknown` there by convention).
  /// </summary>
  public static object Fallback(Type enumType) => _fallbacks.GetOrAdd(enumType, t => {
    var names = WireNames(t);
    if (names.TryGetValue(UnknownName, out var unknown)) return unknown;
    if (names.TryGetValue(NoneName, out var none)) return none;
    return Enum.ToObject(t, 0);
  });

  public static TEnum Fallback<TEnum>() where TEnum : struct, Enum => (TEnum) Fallback(typeof(TEnum));

  /// <summary>Parses a wire name, a C# name or a numeric string. False when none of them match.</summary>
  public static bool TryParse(Type enumType, string? val, out object result) {
    result = Fallback(enumType);
    if (string.IsNullOrWhiteSpace(val)) return false;
    string str = val!.Trim();
    if (WireNames(enumType).TryGetValue(str, out var named)) {
      result = named;
      return true;
    }
    if (long.TryParse(str, out long num)) {
      var boxed = Enum.ToObject(enumType, num);
      // An undefined number is as much a forward-compat signal as an unknown name.
      if (!IsRepresentable(enumType, boxed)) return false;
      result = boxed;
      return true;
    }
    return false;
  }

  public static bool TryParse<TEnum>(string? val, out TEnum result) where TEnum : struct, Enum {
    bool ok = TryParse(typeof(TEnum), val, out object boxed);
    result = (TEnum) boxed;
    return ok;
  }

  /// <summary>Parses a wire value, degrading to <see cref="Fallback(Type)" /> (with a warning) if unknown.</summary>
  public static object Parse(Type enumType, string? val) {
    if (TryParse(enumType, val, out object result)) return result;
    Warn(enumType, val, result);
    return result;
  }

  public static TEnum Parse<TEnum>(string? val) where TEnum : struct, Enum => (TEnum) Parse(typeof(TEnum), val);

  /// <summary>Same, for an enum that arrived as a number (JSON `1`, a packed byte).</summary>
  public static object FromNumber(Type enumType, long num) {
    var boxed = Enum.ToObject(enumType, num);
    if (IsRepresentable(enumType, boxed)) return boxed;
    var fallback = Fallback(enumType);
    Warn(enumType, num.ToString(), fallback);
    return fallback;
  }

  /// <summary>
  /// Whether a numeric value names something this build understands. `[Flags]` enums are excluded:
  /// a legal combination of known flags is not a declared member, so `Enum.IsDefined` says no to it.
  /// </summary>
  private static bool IsRepresentable(Type enumType, object boxed) =>
    Enum.IsDefined(enumType, boxed) || enumType.IsDefined(typeof(FlagsAttribute), false);

  private static void Warn(Type enumType, string? val, object fallback) {
    // Once per (type, value): a bad field in a paged list would otherwise log on every row.
    if (!_warned.TryAdd($"{enumType.FullName}={val}", true)) return;
    ZEnv.Log.Warning("[ENUM] {type} has no value {val} (this build is older than the server's) — using {fallback}",
      enumType.Name, val, fallback);
  }
}

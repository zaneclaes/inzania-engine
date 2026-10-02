#region

using System;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using IZ.Core.Auth;

#endregion

namespace IZ.Client.GoogleAnalytics;

/// <summary>
/// The Measurement Protocol's top-level `device` object: the only place GA reads device data from a
/// non-gtag request (https://developers.google.com/analytics/devguides/collection/protocol/ga4/reference#payload_device_info).
/// Device keys inside an event's `params` are just unregistered event parameters, so they never go there.
///
/// <see cref="From" /> is the one mapping from an <see cref="Installation" /> to it. A browser or unknown install
/// gets none: the page's gtag reports the real browser's device, and a second object would overwrite it.
/// Values follow what GA's own web tag already reports in the property (`Macintosh`, `Windows`, `Linux`, `iOS`,
/// `Android`); a value that cannot be read is omitted, never guessed.
/// </summary>
public class GaDevice {
  public const string DesktopBrowserLabel = "Chordzy Desktop";
  public const string MobileBrowserLabel = "Chordzy Mobile";

  [JsonPropertyName("category")] public string? Category { get; set; } // desktop, mobile, tablet
  [JsonPropertyName("language")] public string? Language { get; set; } // en, en-US
  [JsonPropertyName("screen_resolution")] public string? ScreenResolution { get; set; } // WIDTHxHEIGHT
  [JsonPropertyName("operating_system")] public string? OperatingSystem { get; set; } // Macintosh
  [JsonPropertyName("operating_system_version")] public string? OperatingSystemVersion { get; set; } // 14.5.0
  [JsonPropertyName("model")] public string? Model { get; set; }
  [JsonPropertyName("brand")] public string? Brand { get; set; } // Apple
  [JsonPropertyName("browser")] public string? Browser { get; set; } // Chordzy Desktop
  [JsonPropertyName("browser_version")] public string? BrowserVersion { get; set; } // the app's version

  private static readonly Regex VersionToken = new Regex(@"^\d+(\.\d+)*$", RegexOptions.CultureInvariant);
  private static readonly Regex WindowsBuild = new Regex(@"\((\d+(\.\d+)+)\)", RegexOptions.CultureInvariant);
  private static readonly Regex LanguageTag = new Regex(@"^[A-Za-z]{2,3}(-[A-Za-z0-9]{2,8})*$", RegexOptions.CultureInvariant);

  /// <summary>The device a native install reports, or null for a browser/WebGL or unknown install.</summary>
  public static GaDevice? From(Installation? install) {
    if (install == null) return null;
    string? category = install.DeviceType switch {
      DeviceType.Desktop => "desktop",
      DeviceType.Mobile => "mobile",
      _ => null,
    };
    if (category == null) return null;

    string? os = OperatingSystemOf(install.OsFamily, install.Os);
    return new GaDevice {
      Category = category,
      Language = LanguageOf(install.Language),
      ScreenResolution = install.ScreenWidth > 0 && install.ScreenHeight > 0 ? $"{install.ScreenWidth}x{install.ScreenHeight}" : null,
      OperatingSystem = os,
      OperatingSystemVersion = OperatingSystemVersionOf(os, install.Os),
      Model = NonEmpty(install.Model),
      Brand = os == "Macintosh" || os == "iOS" ? "Apple" : null,
      Browser = install.DeviceType == DeviceType.Mobile ? MobileBrowserLabel : DesktopBrowserLabel,
      BrowserVersion = NonEmpty(install.Version),
    };
  }

  /// <summary>GA's spelling of the OS: Unity's `operatingSystemFamily` (`MacOSX`, `Windows`, `Linux`, and `Other` on
  /// mobile, where the `operatingSystem` string names it).</summary>
  public static string? OperatingSystemOf(string? family, string? os) {
    string f = (family ?? string.Empty).Replace(" ", string.Empty);
    string o = (os ?? string.Empty).TrimStart();
    if (f.StartsWith("MacOS", StringComparison.OrdinalIgnoreCase) || o.StartsWith("Mac OS", StringComparison.OrdinalIgnoreCase) ||
        o.StartsWith("macOS", StringComparison.OrdinalIgnoreCase))
      return "Macintosh";
    if (f.Equals("Windows", StringComparison.OrdinalIgnoreCase) || o.StartsWith("Windows", StringComparison.OrdinalIgnoreCase))
      return "Windows";
    if (o.StartsWith("iOS", StringComparison.OrdinalIgnoreCase) || o.StartsWith("iPadOS", StringComparison.OrdinalIgnoreCase))
      return "iOS";
    if (o.StartsWith("Android", StringComparison.OrdinalIgnoreCase)) return "Android";
    if (f.Equals("Linux", StringComparison.OrdinalIgnoreCase) || o.StartsWith("Linux", StringComparison.OrdinalIgnoreCase))
      return "Linux";
    return null;
  }

  /// <summary>The version inside Unity's `operatingSystem` string: Windows' build in parentheses
  /// (`Windows 11  (10.0.22631) 64bit` → `10.0.22631`, never `64bit`), otherwise the first numeric token
  /// (`Mac OS X 14.5.0`, `iOS 17.0`, `Android OS 14 / API-34 (…)`). Unparseable is null.</summary>
  public static string? OperatingSystemVersionOf(string? gaOs, string? os) {
    if (string.IsNullOrWhiteSpace(os) || gaOs == null) return null;
    if (gaOs == "Windows") {
      Match m = WindowsBuild.Match(os);
      return m.Success ? m.Groups[1].Value : null;
    }
    foreach (string token in os!.Split(new[] { ' ', '/', '(', ')' }, StringSplitOptions.RemoveEmptyEntries)) {
      if (VersionToken.IsMatch(token)) return token;
    }
    return null;
  }

  private static string? LanguageOf(string? language) {
    if (string.IsNullOrWhiteSpace(language)) return null;
    string tag = language!.Trim().Replace('_', '-');
    if (!LanguageTag.IsMatch(tag) || tag.Equals("und", StringComparison.OrdinalIgnoreCase)) return null;
    return tag;
  }

  private static string? NonEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value!.Trim();
}

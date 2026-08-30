using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using IZ.Core.Data;
using IZ.Core.Data.Attributes;
using IZ.Core.Observability.Analytics;
using IZ.Core.Utils;

namespace IZ.Core.Auth;

public enum DeviceType {
  Unknown,
  Browser,
  Desktop,
  Mobile,
}

public class Installation : TransientObject {
  public const int ClientIdLength = 128;
  public const int TimeZoneLength = 128;
  private SemVersion? _semVersion;

  [MaxLength(ClientIdLength)] public string ClientId { get; set; } = null!;

  [MaxLength(TimeZoneLength)] public string? TimeZone { get; set; }

  [MaxLength(128)] public string? Language { get; set; }

  [MaxLength(128)] public string Name { get; set; } = null!;

  [MaxLength(64)] public string Os { get; set; } = null!;

  [MaxLength(64)] public string OsFamily { get; set; } = null!;

  [MaxLength(64)] public string Model { get; set; } = null!;

  [MaxLength(32)] public string Processor { get; set; } = null!;

  public DeviceType DeviceType { get; set; }

  public uint LaunchNumber { get; set; } // Used in analytics

  public int ProcessorCount { get; set; }

  public int Memory { get; set; }

  // Graphics

  [MaxLength(96)] public string GraphicsName { get; set; } = null!;

  [MaxLength(32)] public string GraphicsType { get; set; } = null!;

  public int GraphicsMemory { get; set; }

  // Screen

  public int ScreenWidth { get; set; }

  public int ScreenHeight { get; set; }

  public short ScreenDpi { get; set; }

  // App

  public string Version { get; set; } = null!;

  [JsonIgnore] [OutputIgnore] public SemVersion SemVer {
    // Web/Blazor installs have no version source and historically left Version null,
    // which crashed client Startup with an NRE; fall back to 0.0.0 instead.
    get => _semVersion ??= SemVersion.Parse(string.IsNullOrWhiteSpace(Version) ? "0.0.0" : Version);
    set {
      _semVersion = value;
      Version = value.ToString();
    }
  }

  public static string GetInstallIdForUserId(string userId, string clientId) =>
    clientId.ToSecureAlphanumericHash(userId.ToMd5Hash());

  public string GetInstallIdForUserId(string userId) =>
    GetInstallIdForUserId(userId, ClientId);
}

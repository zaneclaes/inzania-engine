using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using IZ.Core.Data;
using IZ.Core.Data.Attributes;
using IZ.Core.Observability.Analytics;
using Semver;

namespace IZ.Core.Auth;

public class Installation : TransientObject {
  public const int ClientIdLength = 128;
  private SemVersion? _semVersion;

  [MaxLength(ClientIdLength)] public string ClientId { get; set; } = null!;

  [MaxLength(128)] public string? Language { get; set; }

  [MaxLength(128)] public string Name { get; set; } = null!;

  [MaxLength(64)] public string Os { get; set; } = null!;

  [MaxLength(64)] public string OsFamily { get; set; } = null!;

  [MaxLength(64)] public string Model { get; set; } = null!;

  [MaxLength(32)] public string Processor { get; set; } = null!;

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

  [JsonIgnore] [ApiIgnore] public SemVersion SemVer {
    get => _semVersion ??= SemVersion.Parse(Version);
    set {
      _semVersion = value;
      Version = value.ToString();
    }
  }
}

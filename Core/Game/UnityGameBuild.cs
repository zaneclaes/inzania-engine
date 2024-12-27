#region

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using IZ.Core.Data;
using IZ.Core.Data.Attributes;

#endregion

namespace IZ.Core.Game;

public class UnityGameBuild : ModelNumber {
  [ApiDocs("The CI/build number (may be zero for manual)")]
  public ulong BuildNumber { get; set; }

  [ApiDocs("The type of build")]
  [Column(TypeName = "varchar(32)")] public IZEnvironment Env { get; set; } = IZEnvironment.Staging;

  [ApiDocs("Full semantic version (major.minor.patch-tag.buildNumber)")] [JsonIgnore]
  public string Semver => $"{VersionMajor}.{VersionMinor}.{VersionPatch}-{Env.ToString().ToLowerInvariant()}.{BuildNumber}";

  [ApiDocs("Semver's first digit")]
  public ushort VersionMajor { get; set; }

  [ApiDocs("Semver's second digit")]
  public ushort VersionMinor { get; set; }

  [ApiDocs("Semver's third digit")]
  public ushort VersionPatch { get; set; }

  [ApiDocs("The web app's data unityweb file")]
  [MaxLength(128)] public string WebDataFileHash { get; set; } = default!;

  [ApiDocs("The web app's code unityweb file")]
  [MaxLength(128)] public string WebCodeFileHash { get; set; } = default!;

  [ApiDocs("The web app's framework unityweb file")]
  [MaxLength(128)] public string WebFrameworkFileHash { get; set; } = default!;
}

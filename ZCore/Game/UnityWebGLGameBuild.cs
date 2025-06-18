using System.ComponentModel.DataAnnotations;
using IZ.Core.Data.Attributes;

namespace IZ.Core.Game;

public class UnityWebGLGameBuild : UnityGameBuild {
  [ApiDocs("The web app's data unityweb file")]
  [MaxLength(128)] public string WebDataFileHash { get; set; } = null!;

  [ApiDocs("The web app's code unityweb file")]
  [MaxLength(128)] public string WebCodeFileHash { get; set; } = null!;

  [ApiDocs("The web app's framework unityweb file")]
  [MaxLength(128)] public string WebFrameworkFileHash { get; set; } = null!;
}

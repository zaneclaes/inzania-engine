using System.Security.Claims;
using System.Security.Principal;
using IZ.Core.Data.Attributes;

namespace IZ.Core.Auth;

[ApiDocs("Chordzy-specific IIdentity")]
public interface ITuneIdentity : IIdentity {
  public string InstallId { get; }

  public string SessionId { get; }

  [ApiDocs("If in user mode, who, if anybody?")]
  public ITuneSession? UserSession { get; }

  public ITuneUser IZUser { get; }

  [ApiDocs("A principal representing this identity")]
  public ClaimsPrincipal Principal { get; }
}


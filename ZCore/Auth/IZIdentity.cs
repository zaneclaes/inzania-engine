using System.Security.Claims;
using System.Security.Principal;
using IZ.Core.Data.Attributes;

namespace IZ.Core.Auth;

[ApiDocs("App-specific IIdentity")]
public interface IZIdentity : IIdentity {
  public string InstallId { get; }

  public string SessionId { get; }

  [ApiDocs("If in user mode, who, if anybody?")]
  public IZSession? UserSession { get; }

  public IZUser? IZUser { get; }

  [ApiDocs("A principal representing this identity")]
  public ClaimsPrincipal Principal { get; }
}


using System.Security.Claims;
using System.Security.Principal;
using IZ.Core.Data.Attributes;

namespace IZ.Core.Auth;

[ApiDocs("App-specific IIdentity")]
public interface IZIdentity : IIdentity {
  public string ClientId { get; }

  public string SessionId { get; }

  // Could be IP address, or similar
  public string? AddressId { get; }

  [ApiDocs("If in user mode, who, if anybody?")]
  public IZSession? UserSession { get; }

  // Most recent user data
  public IZUser? IZUser { get; set; }

  [ApiDocs("A principal representing this identity")]
  public ClaimsPrincipal Principal { get; }
}

using System.Security.Claims;
using System.Security.Principal;
using IZ.Core.Contexts;
using IZ.Core.Data;
using IZ.Core.Data.Attributes;

namespace IZ.Core.Auth;

public class ZVisitorIdentity : TransientObject, IZIdentity {
  public string? AuthenticationType => GetType().Name.Replace("Identity", "");

  public bool IsAuthenticated => false;

  public string? Name => null;

  public string InstallId { get; }

  public string SessionId { get; }

  public IZSession? UserSession => null;

  [ApiIgnore]
  public IZUser? IZUser => null;

  public ClaimsPrincipal Principal { get; }

  public ZVisitorIdentity(IZContext context, string installId, string? sessionId = null, params ClaimsIdentity[] identities) : base(context) {
    Principal = new GenericPrincipal(this, new[] {
      ZUserRole.Visitor.ToString()
    });
    Principal.AddIdentities(identities);
    InstallId = installId;
    SessionId = sessionId ?? ModelId.GenerateId();
  }
}

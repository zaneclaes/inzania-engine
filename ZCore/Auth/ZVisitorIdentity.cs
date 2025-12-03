using System;
using System.Security.Claims;
using System.Security.Principal;
using IZ.Core.Contexts;
using IZ.Core.Data;
using IZ.Core.Data.Attributes;
using IZ.Core.Observability.Analytics;

namespace IZ.Core.Auth;

public class ZVisitorIdentity : TransientObject, IZIdentity {

  public ZVisitorIdentity(IZContext context, string clientId, string? ipAddress, string? sessionId = null, params ClaimsIdentity[] identities) : base(context) {
    Principal = new GenericPrincipal(this, new[] {
      ZUserRole.Visitor.ToString()
    });
    Principal.AddIdentities(identities);
    ClientId = clientId;
    AddressId = ipAddress;
    SessionId = sessionId ?? ModelId.GenerateId();
  }
  public string? AuthenticationType => GetType().Name.Replace("Identity", "");

  public bool IsAuthenticated => false;

  public string? Name => null;

  public string ClientId { get; }

  public string SessionId { get; }

  public string? AddressId { get; }

  public IZSession? UserSession => null;

  [OutputIgnore]
  public IZUser? IZUser {
    get => null;
    set {
      if (value != null) throw new ArgumentException($"Visitors may not have users");
    }
  }

  public ClaimsPrincipal Principal { get; }
}

using System;
using System.Linq;
using System.Security.Claims;
using IZ.Core.Auth;

namespace IZ.Server.Graphql;

public class PrincipalIdentity {
  public string Id { get; }

  public string Username { get; }

  public string Email { get; }

  public ZUserRole Role { get; }

  public string AuthToken { get; }

  public ClaimsIdentity[] Identities { get; }

  public PrincipalIdentity(ClaimsPrincipal principal, string authToken) {
    Id = principal.GetClaim("nameidentifier");
    Username = principal.GetClaim("preferred_username");
    Email = principal.GetClaim("emailaddress");
    Role = principal.GetTuneRole();
    Identities = principal.Identities.ToArray();
    AuthToken = authToken;
  }

  public PrincipalIdentity(string id, string username, string email, ZUserRole role, string authToken) {
    Id = id;
    Username = username;
    Email = email;
    Role = role;
    Identities = Array.Empty<ClaimsIdentity>();
    AuthToken = authToken;
  }
}

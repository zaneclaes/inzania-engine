#region

using System.Security.Claims;
using System.Threading.Tasks;
using IZ.Core.Auth.Args;
using IZ.Core.Contexts;
using IZ.Core.Data;

#endregion

namespace IZ.Core.Auth;

public interface IZAuthenticator : IHaveContext {
  public Task<IZIdentity> Authenticate(IZContext context, string? clientId, string? authToken, ClaimsPrincipal user);
}

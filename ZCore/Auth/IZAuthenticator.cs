#region

using System.Security.Claims;
using System.Threading.Tasks;
using IZ.Core.Auth.Args;
using IZ.Core.Contexts;

#endregion

namespace IZ.Core.Auth;

public interface IZAuthenticator : IHaveContext {
  public Task<T> Login<T>(LoginArgs args) where T : class, IZSession;

  public Task<T> SignUp<T>(SignUpArgs args) where T : class, IZSession;

  public Task<T> Verify<T>(VerifyUserCodeArgs args) where T : class, IZUser;

  public Task<T> SetUserRole<T>(ChangeUserRoleArgs args) where T : class, IZUser;

  public Task<T> ChangeUsername<T>(ChangeUsernameArgs args) where T : class, IZUser;

  public Task<IZIdentity> Authenticate(IZContext context, string? installId, string? authToken, ClaimsPrincipal user);
}

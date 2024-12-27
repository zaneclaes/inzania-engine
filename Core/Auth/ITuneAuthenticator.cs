#region

using System.Threading.Tasks;
using IZ.Core.Auth.Args;

#endregion

namespace IZ.Core.Auth;

public interface ITuneAuthenticator {
  public Task<T> Login<T>(LoginArgs args) where T : class, ITuneSession;

  public Task<T> SignUp<T>(SignUpArgs args) where T : class, ITuneSession;

  public Task<T> Verify<T>(VerifyUserCodeArgs args) where T : class, ITuneUser;

  public Task<T> SetUserRole<T>(ChangeUserRoleArgs args) where T : class, ITuneUser;

  public Task<T> ChangeUsername<T>(ChangeUsernameArgs args) where T : class, ITuneUser;
}

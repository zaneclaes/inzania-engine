using System;
using System.Threading.Tasks;

namespace IZ.Core.Auth;

// Retrieves the access token for the current user
public interface IStoredUserSession {

  public string? AccessToken { get; }
  public string? Username { get; }
  public ZUserRole? LastRole { get; }

  public event EventHandler<IZSession?> OnUserSessionChanged;

  public IZIdentity? LoadUserSession(Installation install, IZSession? session);

  public Task<IZIdentity> RestoreUserSession(Installation install);
}

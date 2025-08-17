using System;
using System.Threading.Tasks;

namespace IZ.Core.Auth;

// Retrieves the access token for the current user
public interface IStoredUserSession {
  public StoredSession? StoredSession { get; }
  // public string? AccessToken { get; }
  public string? LastUsername { get; }
  public ZUserRole LastRole { get; }

  public event EventHandler<IZSession?> OnUserSessionChanged;

  public IZIdentity? LoadUserSession(Installation install, IZSession? session);

  public Task<IZIdentity> RestoreUserSession(Installation install);
}

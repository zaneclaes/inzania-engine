using System;
using System.Threading.Tasks;

namespace IZ.Core.Auth;

// Retrieves the access token for the current user
public interface IStoredUserSession {
  public event EventHandler<IZSession?> OnUserSessionChanged;

  public string? AccessToken { get; }

  public void LoadUserSession(IZSession? session);

  public Task<IZIdentity> RestoreUserSession(); // Only called if there's a token
}

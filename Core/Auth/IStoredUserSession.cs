using System;

namespace IZ.Core.Auth;

// Retrieves the access token for the current user
public interface IStoredUserSession {
  public event EventHandler<ITuneSession?> OnUserSessionChanged;

  public string? AccessToken { get; }

  public void LoadUserSession(ITuneSession? session);
}

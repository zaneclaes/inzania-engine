using System;
using System.Threading.Tasks;

namespace IZ.Core.Auth;

// Retrieves the access token for the current user
public interface IIdentityStore {
  public event EventHandler<IZIdentity?> OnUserIdentityChanged;

  public IZIdentity? CurrentZIdentity { get; }
  public IZUser? CurrentZUser { get; }

  public StoredSession? StoredSession { get; }
  public LastLoginInfo? LastLogin { get; }

  public IZIdentity? UpdateUserSession(IZSession? session);

  public bool UpdateUserData(IZUser? u);

  protected IZSession? LoadStoredSession();

  // public Task<IZIdentity> RestoreUserSession(Installation install, StoredSession? session = null);
}

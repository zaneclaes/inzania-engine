using System;
using IZ.Core.Contexts;
using IZ.Core.Data;
using IZ.Core.Observability.Analytics;

namespace IZ.Core.Auth;

public abstract class IdentityStore<TU> : LogicBase, IIdentityStore where TU : class, IZUser {
  public event EventHandler<IZIdentity?>? OnUserIdentityChanged;

  public IZIdentity? CurrentZIdentity => _currentIdentity;
  private IZIdentity? _currentIdentity;

  public IZUser? CurrentZUser => _currentIdentity?.IZUser;
  public TU? CurrentUser => _currentIdentity?.IZUser as TU;

  public abstract Installation Install { get; }
  public abstract StoredSession? StoredSession { get; }
  public abstract LastLoginInfo? LastLogin { get; }

  protected abstract IZIdentity CreateIdentity(IZSession session);

  protected abstract void SaveUserData(TU? user);
  protected abstract void WriteUserSession(IZSession? session);
  public abstract IZSession? LoadStoredSession();

  private string _clientId = "";

  public IZIdentity? UpdateUserSession(IZSession? ses) {
    _clientId = Install.ClientId;
    string existingToken = StoredSession?.AccessToken ?? "";
    string newToken = ses?.Token ?? "";

    if (!existingToken.Equals(newToken)) {
      Log.Information("[SESSION] user '{username}' ({id}) now active; saving (token {token})...", ses?.IZUser.Username, ses?.Id, ses?.Token);
      WriteUserSession(ses);
    }
    _currentIdentity = ses == null ? null : CreateIdentity(ses);
    UpdateUserData(ses?.IZUser);
    return _currentIdentity;
  }

  protected virtual void OnUserIdChanged() {
    var analyticsIdentity = _currentIdentity ?? new ZVisitorIdentity(Context, _clientId, null, null);
    Context.GetService<IZAnalytics>()?.SetUserProperties(analyticsIdentity);
    OnUserIdentityChanged?.Invoke(this, _currentIdentity);
  }

  public bool UpdateUserData(IZUser? u) {
    var user = u as TU;
    if (user == null && u != null) {
      Log.Warning("[USER] not tune: {user}", u.GetType());
    }
    bool userChange = CurrentZUser?.Id != user?.Id;
    if (_currentIdentity != null) {
      _currentIdentity.IZUser = user;
    }
    // else Log.Information("[USER] update {user}", user);
    SaveUserData(user);
    if (userChange) OnUserIdChanged();
    return userChange;
  }

  protected IdentityStore(IZContext app) : base(app) {
  }
}

#region

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using IZ.Client.GoogleAnalytics;
using IZ.Core.Auth;
using IZ.Core.Contexts;
using IZ.Core.Observability.Analytics;
using IZ.Core.Utils;
using Microsoft.Extensions.DependencyInjection;
using Tuneality.Core;
using Tuneality.Core.Auth;

#endregion

namespace IZ.Client;

public class ClientContext : RootContext {
  private ITuneChildContext? _span;

  public ZClientApp ClientApp => App as ZClientApp ?? throw new SystemException($"ClientApp is a {App?.GetType()}");

  public override ITuneIdentity? CurrentIdentity => _userIdentity;
  private ITuneIdentity? _userIdentity;

  public ITuneUser? CurrentUser => _userIdentity?.IZUser;
  public TuneUserState? CurrentUserState => _currentUserState ??=
    _userIdentity == null ? null : CreateUserState(_userIdentity.IZUser);
  private TuneUserState? _currentUserState;

  protected virtual TuneUserState? CreateUserState(ITuneUser user) => null;

  public override ITuneAnalytics? Analytics => _analytics ??= new TuneGoogleAnalytics(this);
  private ITuneAnalytics? _analytics;

  protected virtual List<Task> GetStartupTasks() => new List<Task> {
    RestoreSession()
  };

  protected virtual void OnStartupComplete() { }

  public bool IsStarted { get; private set; }

  private bool _isStarting;

  public bool IsShutDown { get; private set; }

  public virtual bool IsRunning => IsStarted && !IsShutDown;

  public Task AwaitStart() => Tasks.WaitUntilAsync(() => IsStarted);

  public async Task Startup(string installId, IAnalyticsSink sink) {
    if (IsStarted) return;
    if (_isStarting) {
      await Tasks.WaitUntilAsync(() => !_isStarting);
      return;
    }
    ClientApp.InstallId = installId;

    _isStarting = true;
    Log.Information("[START] Chordzy starting...");

    await Task.WhenAll(GetStartupTasks().ToArray());
    await Context.Analytics!.Configure(sink, Context.CurrentIdentity);

    if (CurrentIdentity == null) {
      Log.Warning("[START] Chordzy logged out.");
    } else {
      Log.Information("[START] Chordzy ready.");
    }
    IsStarted = true;
    _isStarting = false;
  }

  protected async Task RestoreSession() {
    var storedSession = ServiceProvider.GetRequiredService<IStoredUserSession>();
    if (storedSession.AccessToken == null) {
      _userIdentity = null;
      return;
    }
    try {
      var session = await Context.BeginRequest<AuthQuery>().CurrentSession().Execute();
      _userIdentity = new TuneUserIdentity(this, session);
      storedSession.LoadUserSession(session);
    } catch (Exception e) {
      Log.Warning(e, "Restoring session failed");
      Logout();
    }
  }

  protected void Logout() {
    _userIdentity = null;
    _currentUserState = null;
    ServiceProvider.GetRequiredService<IStoredUserSession>().LoadUserSession(null);
  }

  public ClientContext(ZApp app, IServiceProvider services) : base(app, services) { }

  public override void Dispose() {
    if (!IsShutDown) {
      _analytics?.Dispose();
      _analytics = null;
      base.Dispose();
    }
    IsShutDown = true;
  }
}

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using IZ.Client.GoogleAnalytics;
using IZ.Core.Api;
using IZ.Core.Auth;
using IZ.Core.Auth.Args;
using IZ.Core.Contexts;
using IZ.Core.Data;
using IZ.Core.Observability.Analytics;
using IZ.Core.Utils;
using Microsoft.Extensions.DependencyInjection;
#region

#endregion

namespace IZ.Client;

public abstract class ClientContext : RootContext {

  private readonly Dictionary<string, Stopwatch> _taskTimers = new Dictionary<string, Stopwatch>();
  public TimeSpan Uptime => Context.App.Uptime;

  private IZAnalytics? _analytics;

  private IAnalyticsSink? _analyticsSink;

  public IIdentityStore IdentityStore => _sessionStore ??= this.GetRequiredService<IIdentityStore>();
  private IIdentityStore? _sessionStore;

  protected ClientContext(ZApp app, IServiceProvider services) : base(app, services) {
    // Log.Information("[START] entrypoint...");
  }
  // private IZChildContext? _span;

  public ZClientApp ClientApp => App as ZClientApp ?? throw new SystemException($"ClientApp is a {App?.GetType()}");

  public override IZIdentity? CurrentIdentity => IdentityStore.CurrentZIdentity ?? (_visitorIdentity ??= GetVisitorIdentity());
  private ZVisitorIdentity? _visitorIdentity;

  public override IZAnalytics Analytics => _analytics ??= Context.GetRequiredService<IZAnalytics>();

  public bool IsStarted { get; private set; }

  public bool IsShutDown { get; private set; }

  public virtual bool IsRunning => IsStarted && !IsShutDown;

  public Installation Install { get; protected set; } = null!;

  public Exception? StartupException { get; private set; }

  public bool IsSessionRestored { get; private set; }

  protected virtual ZVisitorIdentity? GetVisitorIdentity() => null;

  protected virtual List<ZTask> GetStartupTasks() => new List<ZTask> {
    RestoreSession()
  };

  protected virtual Dictionary<string, object>? GetUserAnalyticsProperties() => null;

  protected virtual List<ZTask> GetReadyTasks() => new List<ZTask> {
    Context.Analytics!.Configure(_analyticsSink, Install, Context.CurrentIdentity, GetUserAnalyticsProperties())
  };

  public ZTask AwaitStart() => ZTask.WaitUntil(() => IsStarted || StartupException != null);

  public void StartTaskTimer(string taskName, string? functionName = null) {
    if (!string.IsNullOrWhiteSpace(functionName)) taskName = $"{taskName}.{functionName}";
    _taskTimers.Remove(taskName);
    _taskTimers.Add(taskName, Stopwatch.StartNew());
  }

  public void StopTaskTimer(string taskName, string? functionName = null, Exception? exception = null) {
    if (!string.IsNullOrWhiteSpace(functionName)) taskName = $"{taskName}.{functionName}";
    if (!_taskTimers.TryGetValue(taskName, out var timer)) {
      Log.Warning("[TIMER] invalid task timer {name}", taskName);
      return;
    }
    timer.Stop();
    Analytics.OperationTiming(taskName, timer.ElapsedMilliseconds, exception);
    Log.Information("[START] {task} finished in {ms}ms", taskName, timer.ElapsedMilliseconds);
  }

  public async ZTask Startup(Installation install, IAnalyticsSink? sink = null) {
    Log.Information("[START] {installId} starting v{version} after {ms}ms ...",
      install.ClientId, install.SemVer, Uptime.TotalMilliseconds);
    Install = install;
    ClientApp.ClientId = install.ClientId;
    ClientApp.Version = install.SemVer;
    _analyticsSink = sink ?? new GoogleAnalyticsHttpSink(this);

    IsSessionRestored = false;
    StartupException = null;

    try {
      await ZTask.WhenAll(GetStartupTasks().ToArray());
      Log.Information("[START] entering ready state after {ms}ms; breakdown: {tasks}", Uptime.TotalMilliseconds, _taskTimers.Keys.Select(t => $"{t}: {_taskTimers[t].ElapsedMilliseconds}ms"));
      await ZTask.WhenAll(GetReadyTasks().ToArray());
      Log.Information("[START] v{version} ready for {user} after {ms}ms; breakdown: {tasks}", install.SemVer, CurrentIdentity?.UserSession?.IZUser, Uptime.TotalMilliseconds,
        _taskTimers.Keys.Select(t => $"{t}: {_taskTimers[t].ElapsedMilliseconds}ms"));
      IsStarted = true;
    } catch (Exception e) {
      Log.Error(e, "[START] fatal error!");
      StartupException = e;
    } finally {
      IsSessionRestored = true; // just in case... if somehow task didn't execute
    }
  }

  protected async ZTask RestoreSession(bool logoutOnException = true) {
    StartTaskTimer(nameof(RestoreSession));
    try {
      SetCurrentUserSession(await RestoreUserSession());
    } catch (Exception e) {
      Log.Warning(e, "Restoring session failed");
      if (logoutOnException) await Logout();
    } finally {
      IsSessionRestored = true;
      StopTaskTimer(nameof(RestoreSession));
    }
  }

  protected abstract ZTask<IZSession?> RestoreUserSession();

  protected abstract ZTask LogoutUserSession();

  protected void SetCurrentUserSession(IZSession? session) {
    var userIdentity = IdentityStore.UpdateUserSession(session);
    Log.Information("[LOGIN] update session {sessionId} {user}", session, userIdentity);
  }

  protected const string LoginMethod = "password";

  // // effective "test the access token and update user data" function
  // public async ZTask UpdateCurrentUser() {
  //   var user = await Context.BeginRequest<AuthQuery>().CurrentUser().Execute(CommonFormats.Me);
  //   Context.GetRequiredService<TuneClientNavigator>().OnLoginComplete();
  //   Log.Information("[LOGIN]  succeeded: {user}", user);
  // }
  //
  // public async ZTask Login(LoginArgs args) {
  //   Log.Information("[LOGIN] begin login as {username}", args.Username);
  //   Context.Analytics?.LoginBegin(LoginMethod).Forget();
  //   var session = await Context.BeginRequest<AuthMutation>().Login(args, Install).Execute();
  //   var userIdentity = ServiceProvider.GetRequiredService<IStoredUserSession>().LoadUserSession(Install, session);
  //   if (userIdentity == null) {
  //     Log.Information("[LOGIN] NULL id for {session}", session);
  //   } else {
  //     SetCurrentUserIdentity(userIdentity);
  //   }
  //
  //   var user = await Context.BeginRequest<AuthQuery>().CurrentUser().Execute(CommonFormats.Me);
  //   Log.Information("[LOGIN] re-validation succeeded: {user}", user);
  //   Context.GetRequiredService<TuneClientNavigator>().OnLoginComplete();
  //   Context.Analytics?.LoginEnd(LoginMethod).Forget();
  // }
  //
  // public async ZTask Signup(SignUpArgs args) {
  //   Log.Information("[SIGNUP] begin as {email} {username}", args.Email, args.Username);
  //   var session = await Context.BeginRequest<AuthMutation>().SignUp(args, Install).Execute();
  //   SetCurrentUserIdentity(ServiceProvider.GetRequiredService<IStoredUserSession>().LoadUserSession(Install, session)!);
  //   var user = await Context.BeginRequest<AuthQuery>().CurrentUser().Execute(CommonFormats.Me);
  //   Log.Information("[SIGNUP] succeeded: {user}", user);
  //
  //   Context.Analytics?.SignUp(LoginMethod).Forget();
  // }

  public virtual async ZTask Logout() {
    // Context.BeginRequest<AuthMutation>().Logout().Execute().Forget();
    LogoutUserSession().Forget();
    await ZTask.Delay(100); // be 100% certain the logout fired first... but don't need it to finish
    IdentityStore.UpdateUserSession(null);
    Log.Information("[LOGOUT] cleared user identity");
  }

  public override void Dispose() {
    if (!IsShutDown) {
      _analytics?.Dispose();
      _analytics = null;
      base.Dispose();
    }
    IsShutDown = true;
  }
}

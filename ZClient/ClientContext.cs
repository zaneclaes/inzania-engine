using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using IZ.Client.GoogleAnalytics;
using IZ.Core.Auth;
using IZ.Core.Contexts;
using IZ.Core.Observability.Analytics;
using IZ.Core.Utils;
using Microsoft.Extensions.DependencyInjection;
#region

#if Z_UNITY
using Cysharp.Threading.Tasks;
using ZTask = Cysharp.Threading.Tasks.UniTask;
using Tasks = Cysharp.Threading.Tasks.UniTask;
#else
using ZTask = System.Threading.Tasks.Task;
#endif

#endregion

namespace IZ.Client;

public class ClientContext : RootContext {

  private readonly Dictionary<string, Stopwatch> _taskTimers = new Dictionary<string, Stopwatch>();
  public TimeSpan Uptime => Context.App.Uptime;

  private IZAnalytics? _analytics;

  private IAnalyticsSink? _analyticsSink;
  private IZIdentity? _userIdentity;
  private ZVisitorIdentity? _visitorIdentity;

  protected ClientContext(ZApp app, IServiceProvider services) : base(app, services) {
    Log.Information("[START] entrypoint...");
  }
  // private IZChildContext? _span;

  public ZClientApp ClientApp => App as ZClientApp ?? throw new SystemException($"ClientApp is a {App?.GetType()}");

  public override IZIdentity? CurrentIdentity => _userIdentity ?? (_visitorIdentity ??= GetVisitorIdentity());

  public IZUser? CurrentUser => _userIdentity?.IZUser;

  public override IZAnalytics Analytics => _analytics ??= new ZGoogleAnalytics(this);

  public bool IsStarted { get; private set; }

  public bool IsShutDown { get; private set; }

  public virtual bool IsRunning => IsStarted && !IsShutDown;

  public Installation Install { get; private set; } = null!;

  public Exception? StartupException { get; private set; }

  public bool IsSessionRestored { get; private set; }

  protected virtual ZVisitorIdentity? GetVisitorIdentity() => null;

  protected virtual List<ZTask> GetStartupTasks() => new List<ZTask> {
    RestoreSession()
  };

  protected virtual Dictionary<string, object>? GetUserAnalyticsProperties() => null;

  protected virtual List<ZTask> GetReadyTasks() => new List<ZTask> {
    Context.Analytics!.Configure(_analyticsSink, Context.CurrentIdentity, GetUserAnalyticsProperties())
  };

  public ZTask AwaitStart() => Tasks.WaitUntil(() => IsStarted || StartupException != null);

  public void StartTaskTimer(string taskName, string? functionName = null) {
    if (!string.IsNullOrWhiteSpace(functionName)) taskName = $"{taskName}.{functionName}";
    _taskTimers.Remove(taskName);
    _taskTimers.Add(taskName, Stopwatch.StartNew());
  }

  public void StopTaskTimer(string taskName, string? functionName = null) {
    if (!string.IsNullOrWhiteSpace(functionName)) taskName = $"{taskName}.{functionName}";
    if (!_taskTimers.TryGetValue(taskName, out var timer)) {
      Log.Warning("[TIMER] invalid task timer {name}", taskName);
      return;
    }
    timer.Stop();
  }

  public async ZTask Startup(Installation install, IAnalyticsSink? sink = null) {
    Log.Information("[START] {installId} starting v{version} after {ms}ms with {@settings}...",
      install.ClientId, install.SemVer, Uptime.TotalMilliseconds, ClientApp.Settings);
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

  private async ZTask RestoreSession() {
    var storedSession = ServiceProvider.GetRequiredService<IStoredUserSession>();
    // if (storedSession.AccessToken == null) {
    //   _userIdentity = null;
    //   IsSessionRestored = true;
    //   return;
    // }
    _userIdentity = null;
    StartTaskTimer(nameof(RestoreSession));
    try {
      Login(await storedSession.RestoreUserSession(Install));
    } catch (Exception e) {
      Log.Warning(e, "Restoring session failed");
      Logout();
    }
    IsSessionRestored = true;
    StopTaskTimer(nameof(RestoreSession));
  }

  public virtual void Login(IZIdentity userIdentity) {
    Log.Information("[LOGIN] {uid}", userIdentity);
    _userIdentity = userIdentity;
  }

  public virtual void Logout() {
    _userIdentity = null;
    ServiceProvider.GetRequiredService<IStoredUserSession>().LoadUserSession(null);
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

#region

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using IZ.Client.GoogleAnalytics;
using IZ.Core.Auth;
using IZ.Core.Contexts;
using IZ.Core.Observability.Analytics;
using IZ.Core.Utils;
using Microsoft.Extensions.DependencyInjection;

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
  // private IZChildContext? _span;

  public ZClientApp ClientApp => App as ZClientApp ?? throw new SystemException($"ClientApp is a {App?.GetType()}");

  public override IZIdentity? CurrentIdentity => _userIdentity ?? (_visitorIdentity ??= GetVisitorIdentity());
  private IZIdentity? _userIdentity;

  protected virtual ZVisitorIdentity? GetVisitorIdentity() => null;
  private ZVisitorIdentity? _visitorIdentity;

  public IZUser? CurrentUser => _userIdentity?.IZUser;

  public override IZAnalytics? Analytics => _analytics ??= new IzGoogleAnalytics(this);
  private IZAnalytics? _analytics;

  protected virtual List<ZTask> GetStartupTasks() => new List<ZTask> {
    RestoreSession()
  };

  protected virtual List<ZTask> GetReadyTasks() => new List<ZTask> {
    Context.Analytics!.Configure(_analyticsSink, Context.CurrentIdentity)
  };

  private IAnalyticsSink? _analyticsSink;

  public bool IsStarted { get; private set; }

  public bool IsShutDown { get; private set; }

  public virtual bool IsRunning => IsStarted && !IsShutDown;

  public Installation Install { get; private set; } = null!;

  public ZTask AwaitStart() => Tasks.WaitUntil(() => IsStarted || StartupException != null);

  public Exception? StartupException { get; private set; }

  public bool IsSessionRestored { get; private set; }

  public readonly Stopwatch Uptimer;

  private Dictionary<string, Stopwatch> _taskTimers = new Dictionary<string, Stopwatch>();

  public void StartTaskTimer(string taskName, string? functionName = null) {
    if (!string.IsNullOrWhiteSpace(functionName)) taskName = $"{taskName}.{functionName}";
    _taskTimers.Remove(taskName);
    _taskTimers.Add(taskName, Stopwatch.StartNew());
  }

  public void StopTaskTimer(string taskName, string? functionName = null) {
    if (!string.IsNullOrWhiteSpace(functionName)) taskName = $"{taskName}.{functionName}";
    if (!_taskTimers.ContainsKey(taskName)) {
      Log.Warning("[TIMER] invalid task timer {name}", taskName);
      return;
    }
    _taskTimers[taskName].Stop();
  }

  public async ZTask Startup(Installation install, IAnalyticsSink? sink = null) {
    Log.Information("[START] starting v{version} after {ms}ms...", install.SemVer, Uptimer.ElapsedMilliseconds);
    Install = install;
    ClientApp.ClientId = install.ClientId;
    ClientApp.Version = install.SemVer;
    _analyticsSink = sink ?? new GoogleAnalyticsHttpSink(this);

    IsSessionRestored = false;
    StartupException = null;

    try {
      await ZTask.WhenAll(GetStartupTasks().ToArray());
      Log.Information("[START] entering ready state after {ms}ms; breakdown: {tasks}", Uptimer.ElapsedMilliseconds, _taskTimers.Keys.Select(t => $"{t}: {_taskTimers[t].ElapsedMilliseconds}ms"));
      await ZTask.WhenAll(GetReadyTasks().ToArray());
      Log.Information("[START] v{version} ready for {user} after {ms}ms; breakdown: {tasks}", install.SemVer, CurrentIdentity?.UserSession?.IZUser, Uptimer.ElapsedMilliseconds,
        _taskTimers.Keys.Select(t => $"{t}: {_taskTimers[t].ElapsedMilliseconds}ms"));
      IsStarted = true;
    } catch (Exception e) {
      Log.Error(e, "[START] fatal error!");
      StartupException = e;
    } finally {
      IsSessionRestored = true; // just in case... if somehow task didn't execute
      Uptimer.Stop();
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

  protected ClientContext(ZApp app, IServiceProvider services) : base(app, services) {
    Uptimer = Stopwatch.StartNew();
    Log.Information("[START] entrypoint...");
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

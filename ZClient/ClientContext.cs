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
using Semver;

#endregion

namespace IZ.Client;

public class ClientContext : RootContext {
  // private IZChildContext? _span;

  public ZClientApp ClientApp => App as ZClientApp ?? throw new SystemException($"ClientApp is a {App?.GetType()}");

  public override IZIdentity? CurrentIdentity => _userIdentity;
  private IZIdentity? _userIdentity;

  public IZUser? CurrentUser => _userIdentity?.IZUser;

  public override IZAnalytics? Analytics => _analytics ??= new IzGoogleAnalytics(this);
  private IZAnalytics? _analytics;

  protected virtual List<Task> GetStartupTasks() => new List<Task> {
    RestoreSession()
  };

  protected virtual List<Task> GetReadyTasks() => new List<Task> {
    Context.Analytics!.Configure(_analyticsSink, Context.CurrentIdentity)
  };

  private IAnalyticsSink? _analyticsSink;

  protected virtual void OnStartupComplete() { }

  public bool IsStarted { get; private set; }

  private bool _isStarting;

  public bool IsShutDown { get; private set; }

  public virtual bool IsRunning => IsStarted && !IsShutDown;

  public Task AwaitStart() => Tasks.WaitUntilAsync(() => IsStarted);

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

  public async Task Startup(string clientId, SemVersion version, IAnalyticsSink? sink = null) {
    if (IsStarted) return;
    if (_isStarting) {
      await Tasks.WaitUntilAsync(() => !_isStarting);
      return;
    }
    ClientApp.ClientId = clientId;
    ClientApp.Version = version;

    IsSessionRestored = false;
    _isStarting = true;
    _analyticsSink = sink ?? new GoogleAnalyticsHttpSink(this);
    Log.Debug("[START] Chordzy starting after {ms}ms...", Uptimer.ElapsedMilliseconds);;

    try {
      await Task.WhenAll(GetStartupTasks().ToArray());
      // Log.Information("[START] Chordzy entering ready state after {ms}ms; breakdown: {tasks}", Uptimer.ElapsedMilliseconds, _taskTimers.Keys.Select(t => $"{t}: {_taskTimers[t].ElapsedMilliseconds}ms"));
      await Task.WhenAll(GetReadyTasks().ToArray());
      Log.Information("[START] Chordzy v{version} ready for {user} after {ms}ms; breakdown: {tasks}", version, CurrentIdentity?.UserSession?.IZUser, Uptimer.ElapsedMilliseconds,
        _taskTimers.Keys.Select(t => $"{t}: {_taskTimers[t].ElapsedMilliseconds}ms"));
      IsStarted = true;
    } finally {
      Uptimer.Stop();
      _isStarting = false;
    }
  }

  private async Task RestoreSession() {
    var storedSession = ServiceProvider.GetRequiredService<IStoredUserSession>();
    if (storedSession.AccessToken == null) {
      _userIdentity = null;
      IsSessionRestored = true;
      return;
    }
    StartTaskTimer(nameof(RestoreSession));
    try {
      _userIdentity = await storedSession.RestoreUserSession();
    } catch (Exception e) {
      Log.Warning(e, "Restoring session failed");
      Logout();
    }
    IsSessionRestored = true;
    StopTaskTimer(nameof(RestoreSession));
  }

  protected virtual void Logout() {
    _userIdentity = null;
    ServiceProvider.GetRequiredService<IStoredUserSession>().LoadUserSession(null);
  }

  protected ClientContext(ZApp app, IServiceProvider services) : base(app, services) {
    Uptimer = Stopwatch.StartNew();
    Log.Information("[START] Chordzy Entrypoint...");
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

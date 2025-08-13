using System;
using System.Collections.Generic;
using System.Linq;
using IZ.Client.GoogleAnalytics.Events;
using IZ.Core;
using IZ.Core.Auth;
using IZ.Core.Contexts;
using IZ.Core.Data;
using IZ.Core.Observability.Analytics;
using IZ.Core.Utils;
using Tuneality.Core.Clients;
#region

#if Z_UNITY
using Cysharp.Threading.Tasks;
using ZTask = Cysharp.Threading.Tasks.UniTask;
using Tasks = Cysharp.Threading.Tasks.UniTask;
#else
using ZTask = System.Threading.Tasks.Task;
#endif

#endregion

namespace IZ.Client.GoogleAnalytics;

public class ZGoogleAnalytics : LogicBase, IZAnalytics {
  private const double EngagementSampling = 5.0;

  private readonly Queue<AnalyticsEvent> _queue = new Queue<AnalyticsEvent>();

  private string? _path;

  // public static GaStream FallbackStream { get; } = new GaStream("Chordzy Test", "G-MV3MFD3WDH", 8193422753);

  private IAnalyticsSink? _sink;

  private ZVisitorIdentity? _visitor;

  public ZGoogleAnalytics(IZContext context) : base(context) { }

  public AnalyticsOptions StreamOptions => _stream ??= Context.GetRequiredService<TuneClientAppSettings>().GoogleAnalytics;
  private AnalyticsOptions? _stream = null;

  private TimeSpan _lastEngagementTime = TimeSpan.Zero;

  private readonly Dictionary<string, object> _userProps = new Dictionary<string, object>();

  private Dictionary<string, object> MergeUserProps(Dictionary<string, object>? userProps) {
    if (userProps is null) return _userProps;
    foreach (var prop in userProps) {
      _userProps[prop.Key] = prop.Value;
    }
    return _userProps;
  }

  public async ZTask Configure(IAnalyticsSink? sink, IZIdentity? identity = null, Dictionary<string, object>? userProps = null) {
    if (sink == null) return;
    if (identity == null) {
      if (_visitor == null) {
        Log.Warning("[ANALYTICS] falling back on auto-generated identity");
        _visitor = new ZVisitorIdentity(Context, ModelId.GenerateId(), null);
      }
      identity = _visitor;
    }
    await (_sink = sink).Config(StreamOptions, identity.ClientId, identity.IZUser?.Id);
    await ((IZAnalytics) this).SetIdentity(identity, MergeUserProps(userProps));
    ProcessQueue();
  }

  public ZTask SetUserProperties(string installId, string? userId, Dictionary<string, object>? props = null) =>
    _sink?.Config(StreamOptions, installId, userId, MergeUserProps(props)) ?? ZTask.CompletedTask;

  public async ZTask SendEvent<T>(AnalyticsEvent<T> e) where T : IEventParams {
    if (_sink == null) {
      _queue.Enqueue(e);
    } else {
      await _sink.SendEvent(e);
    }
  }

  public ZTask PageView(string path, string? title = null) {
    if (_path == path) return ZTask.CompletedTask;
    _path = path;

    return ((IZAnalytics) this).SendEvent("page_view", new PageViewEventParams {
      Path = path,
      Title = title
    }); // data
  }

  private long? GatherEngagementTime() {
    var ts = Context.App.Uptime;
    var elapsed = ts - _lastEngagementTime;
    if (elapsed.TotalSeconds < EngagementSampling) return null;
    _lastEngagementTime = ts;
    return (long) elapsed.TotalMilliseconds;
  }

  public ZTask UserEngagement() {
    var elapsed = GatherEngagementTime();
    if (!elapsed.HasValue) return ZTask.CompletedTask;

    Log.Debug("[ENG] {msec}msec", elapsed.Value);
    return ((IZAnalytics) this).SendEvent("user_engagement", new UserEngagementEventParams() {
      EngagementTimeMsec = elapsed.Value
    }); // data
  }

  public ZTask ScreenView(string name, string? klass = null) =>
    ((IZAnalytics) this).SendEvent("screen_view", new ScreenViewEventParams {
      Name = name,
      Class = klass,
    }); // data

  public ZTask Share(string method) =>
    ((IZAnalytics) this).SendEvent("share", new MethodEventParams {
      Method = method
    });

  public ZTask LoginBegin(string method) =>
    ((IZAnalytics) this).SendEvent("login_begin", new MethodEventParams {
      Method = method
    });

  public ZTask LoginEnd(string method) =>
    ((IZAnalytics) this).SendEvent("login", new MethodEventParams {
      Method = method
    });

  public ZTask SignUp(string method) =>
    ((IZAnalytics) this).SendEvent("sign_up", new MethodEventParams {
      Method = method
    });

  public ZTask Search(string searchTerm) =>
    ((IZAnalytics) this).SendEvent("search", new SearchEventParams {
      SearchTerm = searchTerm
    });

  public ZTask EarnPoints(long score, int? level = null, string? character = null) =>
    ((IZAnalytics) this).SendEvent("post_score", new ScoreEventParams {
      Score = score,
      Level = level,
      Character = character
    });

  public ZTask SelectContent(string contentType, string contentId) =>
    ((IZAnalytics) this).SendEvent("select_content", new ContentEventParams {
      ContentType = contentType,
      ContentId = contentId
    });

  public ZTask Exception(string desc, bool fatal = false) =>
    ((IZAnalytics) this).SendEvent("exception", new ExceptionEventParams {
      Description = desc,
      IsFatal = fatal
    });

  public override void Dispose() {
    base.Dispose();
    _sink?.Dispose();
    _sink = null;
  }

  private void ProcessQueue() {
    if (_sink == null || !_queue.Any()) return;
    while (_queue.Any()) {
      var o = _queue.Dequeue();
      _sink.SendEvent(o).Forget();
    }
  }
}

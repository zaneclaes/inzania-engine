#region

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using IZ.Client.GoogleAnalytics.Events;
using IZ.Core;
using IZ.Core.Auth;
using IZ.Core.Contexts;
using IZ.Core.Data;
using IZ.Core.Observability.Analytics;
using IZ.Core.Utils;

#endregion

namespace IZ.Client.GoogleAnalytics;

public class IzGoogleAnalytics : LogicBase, IZAnalytics {
  public static AnalyticsStream ProdStream { get; } = new AnalyticsStream("Chordzy", "G-XXGK8DWT14", 8189434535);
  public static AnalyticsStream StagingStream { get; } = new AnalyticsStream("Chordzy Test", "G-MV3MFD3WDH", 8193422753);
  public static AnalyticsStream GetStreamForContext(IZContext context) =>
    context.App.Env <= ZEnvironment.Staging ? StagingStream : ProdStream;

  public AnalyticsStream Stream => GetStreamForContext(Context);

  // public static GaStream FallbackStream { get; } = new GaStream("Chordzy Test", "G-MV3MFD3WDH", 8193422753);

  private IAnalyticsSink? _sink;

  private readonly Queue<AnalyticsEvent> _queue = new Queue<AnalyticsEvent>();

  private string? _path;

  private ZVisitorIdentity? _visitor;

  public async Task Configure(IAnalyticsSink sink, IZIdentity? identity = null) {
    if (identity == null) {
      if (_visitor == null) {
        Log.Warning("[ANALYTICS] falling back on auto-generated identity");
        _visitor = new ZVisitorIdentity(Context, ModelId.GenerateId());
      }
      identity = _visitor;
    }
    await (_sink = sink).Config(Stream, identity.InstallId, identity.SessionId, identity.IZUser?.Id);
    await ((IZAnalytics) this).SetIdentity(identity);
    ProcessQueue();
  }

  public IzGoogleAnalytics(IZContext context) : base(context) { }

  public Task SetUserProperties(string installId, string sessionId, string? userId, Dictionary<string, object> props) =>
    _sink!.Config(Stream, installId, sessionId, userId, props);

  public async Task SendEvent<T>(AnalyticsEvent<T> e) where T : IEventParams {
    if (_sink == null) {
      _queue.Enqueue(e);
    } else {
      await _sink.SendEvent(e);
    }
  }

  private void ProcessQueue() {
    if (_sink == null || !_queue.Any()) return;
    while (_queue.Any()) {
      var o = _queue.Dequeue();
      _sink.SendEvent(o).Forget();
    }
  }

  public Task PageView(string path, string? title = null) {
    if (_path == path) return Task.CompletedTask;
    _path = path;

    return ((IZAnalytics) this).SendEvent("page_view", new PageViewEventParams {
      Path = path,
      Title = title
    }); // data
  }

  public Task ScreenView(string name, string? klass = null) =>
    ((IZAnalytics) this).SendEvent("screen_view", new ScreenViewEventParams {
      Name = name,
      Class = klass
    }); // data

  public Task Share(string method) =>
    ((IZAnalytics) this).SendEvent("share", new MethodEventParams {
      Method = method
    });

  public Task LoginBegin(string method) =>
    ((IZAnalytics) this).SendEvent("login_begin", new MethodEventParams {
      Method = method
    });

  public Task LoginEnd(string method) =>
    ((IZAnalytics) this).SendEvent("login", new MethodEventParams {
      Method = method
    });

  public Task SignUp(string method) =>
    ((IZAnalytics) this).SendEvent("sign_up", new MethodEventParams {
      Method = method
    });

  public Task Search(string searchTerm) =>
    ((IZAnalytics) this).SendEvent("search", new SearchEventParams {
      SearchTerm = searchTerm
    });

  public Task EarnPoints(long score, int? level = null, string? character = null) =>
    ((IZAnalytics) this).SendEvent("post_score", new ScoreEventParams {
      Score = score,
      Level = level,
      Character = character
    });

  public Task SelectContent(string contentType, string contentId) =>
    ((IZAnalytics) this).SendEvent("select_content", new ContentEventParams {
      ContentType = contentType,
      ContentId = contentId
    });

  public Task Exception(string desc, bool fatal = false) =>
    ((IZAnalytics) this).SendEvent("exception", new ExceptionEventParams {
      Description = desc,
      IsFatal = fatal
    });

  public override void Dispose() {
    base.Dispose();
    _sink?.Dispose();
    _sink = null;
  }
}

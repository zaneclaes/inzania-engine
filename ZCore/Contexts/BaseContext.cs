using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using IZ.Core.Auth;
using IZ.Core.Data;
using IZ.Core.Data.Attributes;
using IZ.Core.Observability.Analytics;
using IZ.Core.Observability.Logging;
using IZ.Core.Observability.Metrics;
using Microsoft.Extensions.DependencyInjection;

namespace IZ.Core.Contexts;

public abstract class BaseContext : IZContext, IEventEnricher {
  private readonly IServiceProvider? _services;

  protected readonly string _uuid = ModelId.GenerateId();
  private IZAnalytics? _analytics;

  private CancellationToken? _cancellationToken;
  private IZDataRepository? _data;
  private Dictionary<string, object>? _dataBag;
  private Dictionary<string, object>? _eventProperties;
  private Dictionary<string, object>? _eventTags;

  private IZLogger? _log;
  private IZMetrics? _metrics;

  protected BaseContext(
    ZApp app, IServiceProvider? services = null, IZLogger? logger = null
  ) {
    _services = services;
    App = app;
    if (logger != null) _log = logger.ForContext(GetType(), this);
    // _services?.GetService<IZLogger>();
  }
  public IZSpan Span { get; protected set; } = null!;

  public virtual Dictionary<string, object> EventProperties => _eventProperties ??= BuildEventProperties();

  public Dictionary<string, object> EventTags => _eventTags ??= BuildEventTags();

  [ApiIgnore] public IZContext Context => this;
  [ApiIgnore] public IZLogger Log => _log ??= App.Log.ForContext(GetType(), this);

  [ApiIgnore] public virtual IZMetrics? Metrics => _metrics ??= Parent?.Metrics;

  [ApiIgnore] public virtual IZAnalytics? Analytics => _analytics ??=
    Parent?.Analytics ?? ServiceProvider.GetService<IZAnalytics>();

  [ApiIgnore]
  public virtual IServiceProvider ServiceProvider => _services ?? Parent?.ServiceProvider ?? throw new NullReferenceException(nameof(ServiceProvider));

  public virtual IZIdentity? CurrentIdentity => Parent?.CurrentIdentity;

  public virtual IZDataRepository Data => Parent?.Data ?? (_data ??= this.GetRequiredService<IZDataFactory>().GetDataRepository(this));

  public virtual IZResolver Resolver => Parent?.Resolver ?? throw new NullReferenceException(nameof(Resolver));
  public CancellationToken CancellationToken {
    get => _cancellationToken ??= new CancellationTokenSource().Token;
    set => _cancellationToken = value;
  }

  public virtual string Resource => "Root";

  public virtual string? Action => null;

  public virtual IZContext? Parent => null;

  public ZApp App { get; }

  public virtual IZChildContext ScopeAction(Type? t, string? reason = null, IZLogger? logger = null) => new ActionContext(this, t, reason, logger);

  public Dictionary<string, object> DataBag => Parent?.DataBag ?? (_dataBag ??= new Dictionary<string, object>());

  public virtual void Dispose() {
    _data?.Dispose();
    _data = null;
    Span.Dispose();
  }
  private Dictionary<string, object> BuildEventProperties() => this.GetEventProperties();
  private Dictionary<string, object> BuildEventTags() => EventProperties
    .ToDictionary(k => k.Key.Replace(".", "_").ToLower(), k => k.Value);

  protected void Init() {
    Span = ZEnv.SpanBuilder.Invoke();
  }

  public override string ToString() => $"{GetType().Name}#{_uuid}<{Resource}>{Action}()";
}

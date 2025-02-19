#region

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using IZ.Core.Api;
using IZ.Core.Auth;
using IZ.Core.Data;
using IZ.Core.Data.Attributes;
using IZ.Core.Observability.Analytics;
using IZ.Core.Observability.Logging;
using IZ.Core.Observability.Metrics;
using IZ.Core.Utils;
using Microsoft.Extensions.DependencyInjection;

#endregion

namespace IZ.Core.Contexts;

public abstract class BaseContext : IZContext, IEventEnricher {
  public IZSpan Span { get; protected set; } = null!;

  public virtual Dictionary<string, object> EventProperties => _eventProperties ??= BuildEventProperties();
  private Dictionary<string, object>? _eventProperties;
  private Dictionary<string, object> BuildEventProperties() => this.GetEventProperties();

  public Dictionary<string, object> EventTags => _eventTags ??= BuildEventTags();
  private Dictionary<string, object>? _eventTags;
  private Dictionary<string, object> BuildEventTags() => EventProperties
    .ToDictionary(k => k.Key.Replace(".", "_").ToLower(), k => k.Value);

  private string _uuid = ModelId.GenerateId();

  protected BaseContext(
    ZApp app, IServiceProvider? services = null, IZLogger? logger = null
  ) {
    _services = services;
    App = app;
    if (logger != null) _log = logger.ForContext(GetType(), this);
    // _services?.GetService<IZLogger>();
  }

  protected void Init() {
    Span = ZEnv.SpanBuilder.Invoke(this);
  }

  [ApiIgnore] public IZContext Context => this;

  private IZLogger? _log;
  [ApiIgnore] public IZLogger Log => _log ??= App.Log.ForContext(GetType(), this);

  [ApiIgnore] public virtual IZMetrics? Metrics => _metrics ??= Parent?.Metrics;
  private IZMetrics? _metrics;

  [ApiIgnore] public virtual IZAnalytics? Analytics => _analytics ??=
    Parent?.Analytics ?? ServiceProvider.GetService<IZAnalytics>();
  private IZAnalytics? _analytics;

  [ApiIgnore]
  public virtual IServiceProvider ServiceProvider => _services ?? Parent?.ServiceProvider ?? throw new NullReferenceException(nameof(ServiceProvider));
  private readonly IServiceProvider? _services;

  public virtual IZIdentity? CurrentIdentity => Parent?.CurrentIdentity;

  public virtual IZDataRepository Data => Parent?.Data ?? (_data ??= ServiceProvider.GetRequiredService<IZDataFactory>().GetDataRepository(this));
  private IZDataRepository? _data;

  public virtual IZResolver Resolver => Parent?.Resolver ?? throw new NullReferenceException(nameof(Resolver));

  private CancellationToken? _cancellationToken;
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
  private Dictionary<string, object>? _dataBag = null;

  public virtual void Dispose() {
    _data?.Dispose();
    _data = null;
    Span.Dispose();
  }

  public override string ToString() => $"{GetType().Name}#{_uuid}<{Resource}>{Action}()";
}

public class RootContext : BaseContext, IZRootContext {

  public RootContext(
    ZApp app, IServiceProvider services
  ) : base(app, services) {
    Init();
  }

  public override IZResolver Resolver => _resolver ??=
    ServiceProvider.GetService<IProvideRootContext>()?.GetResolver(this) ?? new ZDefaultResolver(this);

  private IZResolver? _resolver;

  public override void Dispose() {
    base.Dispose();
  }
}

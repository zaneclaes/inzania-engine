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

public abstract class BaseContext : ITuneContext, IEventEnricher {
  public ITuneSpan Span { get; protected set; } = default!;

  public virtual Dictionary<string, object> EventProperties => _eventProperties ??= BuildEventProperties();
  private Dictionary<string, object>? _eventProperties;
  private Dictionary<string, object> BuildEventProperties() => this.GetEventProperties();

  public Dictionary<string, object> EventTags => _eventTags ??= BuildEventTags();
  private Dictionary<string, object>? _eventTags;
  private Dictionary<string, object> BuildEventTags() => EventProperties
    .ToDictionary(k => k.Key.Replace(".", "_").ToLower(), k => k.Value);

  private string _uuid = ModelId.GenerateId();

  protected BaseContext(
    ZApp app, IServiceProvider? services = null, ITuneLogger? logger = null
  ) {
    _services = services;
    App = app;
    if (logger != null) _log = logger.ForContext(GetType(), this);
    // _services?.GetService<ITuneLogger>();
  }

  protected void Init() {
    Span = IZEnv.SpanBuilder.Invoke(this);
    // Log.Information("[ROOT] create {context}", ToString());//, new TuneTrace(new StackTrace().ToString()).ToString());
  }

  [ApiIgnore] public ITuneContext Context => this;

  private ITuneLogger? _log;
  [ApiIgnore] public ITuneLogger Log => _log ??= App.Log.ForContext(GetType(), this);

  [ApiIgnore] public virtual ITuneMetrics? Metrics => _metrics ??= Parent?.Metrics;
  private ITuneMetrics? _metrics;

  [ApiIgnore] public virtual ITuneAnalytics? Analytics => _analytics ??=
    Parent?.Analytics ?? ServiceProvider.GetService<ITuneAnalytics>();
  private ITuneAnalytics? _analytics;

  [ApiIgnore]
  public virtual IServiceProvider ServiceProvider => _services ?? Parent?.ServiceProvider ?? throw new NullReferenceException(nameof(ServiceProvider));
  private readonly IServiceProvider? _services;

  public virtual ITuneIdentity? CurrentIdentity => Parent?.CurrentIdentity;

  public virtual ITuneDataRepository Data => Parent?.Data ?? (_data ??= ServiceProvider.GetRequiredService<ITuneDataFactory>().GetDataRepository(this));
  private ITuneDataRepository? _data;

  public virtual ITuneResolver Resolver => Parent?.Resolver ?? throw new NullReferenceException(nameof(Resolver));

  // [ApiIgnore]
  // public virtual ITuneRequest Request =>
  //   new TuneRequest(this);

  private CancellationToken? _cancellationToken;
  public CancellationToken CancellationToken {
    get => _cancellationToken ??= new CancellationTokenSource().Token;
    set => _cancellationToken = value;
  }

  public virtual string Resource => "Root";

  public virtual string? Action => null;

  public virtual ITuneContext? Parent => null;

  public ZApp App { get; }

  public virtual ITuneChildContext ScopeAction(Type? t, string? reason = null, ITuneLogger? logger = null) => new ActionContext(this, t, reason, logger);

  public virtual void Dispose() {
    _data?.Dispose();
    _data = null;
    Span.Dispose();
    // Log.Information("[ROOT] dispose {context}", ToString());//, new TuneTrace(new StackTrace().ToString()).ToString());
  }

  public override string ToString() => $"{GetType().Name}#{_uuid}<{Resource}>{Action}()";
}

public class RootContext : BaseContext, ITuneRootContext {

  public RootContext(
    ZApp app, IServiceProvider services
  ) : base(app, services) {
    Init();
  }

  public override ITuneResolver Resolver => _resolver ??=
    ServiceProvider.GetService<IProvideRootContext>()?.GetResolver(this) ?? new TuneDefaultResolver(this);

  private ITuneResolver? _resolver;

  public override void Dispose() {
    base.Dispose();
  }
}

#region

using System;
using System.Threading;
using IZ.Core.Api;
using IZ.Core.Auth;
using IZ.Core.Data;
using IZ.Core.Data.Attributes;
using IZ.Core.Observability.Analytics;
using IZ.Core.Observability.Logging;
using IZ.Core.Observability.Metrics;
using Microsoft.Extensions.DependencyInjection;

#endregion

namespace IZ.Core.Contexts;

public interface ITuneContext : IServiceScope, IAmInternal, IEventEnricher {
  // public ITuneLogger Log { get; }

  [ApiDocs("The application context in which we are running (defaults to IZEnv.App)")]
  public ZApp App { get; }

  public ITuneAnalytics? Analytics { get; }

  public ITuneMetrics? Metrics { get; }

  [ApiDocs("The name of the class resource that is scoped by the span")]
  public string Resource { get; }

  [ApiDocs("The verb/operation being performed")]
  public string? Action { get; }

  [ApiDocs("The user or system responsible for the current action")]
  public ITuneIdentity? CurrentIdentity { get; }

  [ApiDocs("Get the object which provides the LOCAL data (sqlite, filesystem, local MySQL, etc.)")]
  public ITuneDataRepository Data { get; }

  [ApiDocs("The batched data loader")]
  public ITuneResolver Resolver { get; }

  // [ApiDocs("The request executor, allowing access to API and/or cached local data")]
  // public ITuneRequest Request { get; }

  [ApiDocs("The request cancellation token")]
  public CancellationToken CancellationToken { get; set; }

  [ApiDocs("The tracer/diagonstic span")]
  public ITuneSpan? Span { get; }

  [ApiDocs("Child contexts always have parents")]
  public ITuneContext? Parent { get; }

  public ITuneRootContext Root => this is ITuneRootContext rc ? rc : Parent?.Root ?? throw new SystemException($"{GetType()} has no root parent");

  [ApiDocs("Spawn a child context (span) for some action")]
  public ITuneChildContext ScopeAction(Type? t, string? reason = null, ITuneLogger? logger = null);
}

public interface ITuneRootContext : ITuneContext { }
public interface ITuneBackgroundContext : ITuneRootContext { }
public interface ITuneChildContext : ITuneContext { }

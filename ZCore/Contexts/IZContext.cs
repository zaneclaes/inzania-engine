#region

using System;
using System.Collections.Generic;
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

public interface IZContext : IServiceScope, IAmInternal, IEventEnricher {
  // public IZLogger Log { get; }

  [ApiDocs("The application context in which we are running (defaults to ZEnv.App)")]
  public ZApp App { get; }

  public IZAnalytics? Analytics { get; }

  public IZMetrics? Metrics { get; }

  [ApiDocs("The name of the class resource that is scoped by the span")]
  public string Resource { get; }

  [ApiDocs("The verb/operation being performed")]
  public string? Action { get; }

  [ApiDocs("The user or system responsible for the current action")]
  public IZIdentity? CurrentIdentity { get; }

  [ApiDocs("Get the object which provides the LOCAL data (sqlite, filesystem, local MySQL, etc.)")]
  public IZDataRepository Data { get; }

  [ApiDocs("The batched data loader")]
  public IZResolver Resolver { get; }

  [ApiDocs("The request cancellation token")]
  public CancellationToken CancellationToken { get; set; }

  [ApiDocs("The tracer/diagonstic span")]
  public IZSpan? Span { get; }

  [ApiDocs("Child contexts always have parents")]
  public IZContext? Parent { get; }

  public IZRootContext Root => this is IZRootContext rc ? rc : Parent?.Root ?? throw new SystemException($"{GetType()} has no root parent");

  [ApiDocs("Spawn a child context (span) for some action")]
  public IZChildContext ScopeAction(Type? t, string? reason = null, IZLogger? logger = null);

  public Dictionary<string, object> DataBag { get; }
}

public interface IZRootContext : IZContext { }
public interface IZBackgroundContext : IZRootContext { }
public interface IZChildContext : IZContext { }

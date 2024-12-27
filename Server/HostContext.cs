#region

using System;
using System.Diagnostics;
using IZ.Core.Auth;
using IZ.Core.Contexts;
using IZ.Core.Observability.Metrics;
using IZ.Core.Utils;
using IZ.Observability.DataDog;
using Microsoft.AspNetCore.Http;

#endregion

namespace IZ.Server;

public class HostContext : RootContext {
  public override ITuneIdentity? CurrentIdentity => HttpContext?.User.Identity as ITuneIdentity;

  private readonly IHttpContextAccessor? _httpContextAccessor;

  public override ITuneMetrics? Metrics => _metrics ??= new DataDogMetrics(this);
  private ITuneMetrics? _metrics;

  private HttpContext? _httpContext;
  public HttpContext? HttpContext {
    get => _httpContext ??= _httpContextAccessor?.HttpContext;
    internal set => _httpContext = value;
  }

  // public override IServiceProvider ServiceProvider => HttpContext?.RequestServices ?? base.ServiceProvider;

  public HostContext(
    ZApp app,
    IServiceProvider services,
    IHttpContextAccessor? http
  ) : base(app, services) {
    Log.Verbose("[STACK] {trace}", new TuneTrace(new StackTrace().ToString()).ToString());
    _httpContextAccessor = http;
    if (HttpContext != null) ((DataDogSpan?) Span)?.Span.SetTag("http_trace_id", HttpContext.TraceIdentifier);
  }

  public HostContext(
    ZApp app,
    IServiceProvider services,
    HttpContext httpContext
  ) : base(app, services) {
    Log.Verbose("[STACK] {trace}", new TuneTrace(new StackTrace().ToString()).ToString());
    _httpContext = httpContext;
    if (HttpContext != null) ((DataDogSpan?) Span)?.Span.SetTag("http_trace_id", HttpContext.TraceIdentifier);
  }
}

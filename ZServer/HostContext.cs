#region

using System;
using System.Diagnostics;
using HotChocolate.Execution;
using IZ.Core.Auth;
using IZ.Core.Contexts;
using IZ.Core.Observability.Metrics;
using IZ.Core.Utils;
using IZ.Observability.DataDog;
using Microsoft.AspNetCore.Http;

#endregion

namespace IZ.Server;

public class HostContext : RootContext {

  private readonly IHttpContextAccessor? _httpContextAccessor;

  private HttpContext? _httpContext;

  public HostContext(
    ZApp app,
    IServiceProvider services,
    IHttpContextAccessor? http
  ) : base(app, services) {
    Log.Verbose("[STACK] {trace}", new ZTrace(new StackTrace().ToString()).ToString());
    _httpContextAccessor = http;
    if (HttpContext != null) ((DataDogSpan?) Span)?.Span.SetTag("http_trace_id", HttpContext.TraceIdentifier);
  }

  public HostContext(
    ZApp app,
    IServiceProvider services,
    HttpContext httpContext
  ) : base(app, services) {
    Log.Verbose("[STACK] {trace}", new ZTrace(new StackTrace().ToString()).ToString());
    _httpContext = httpContext;
    if (HttpContext != null) ((DataDogSpan?) Span)?.Span.SetTag("http_trace_id", HttpContext.TraceIdentifier);
  }
  public override IZIdentity? CurrentIdentity => HttpContext?.User.Identity as IZIdentity;

  public HttpContext? HttpContext {
    get => _httpContext ??= _httpContextAccessor?.HttpContext;
    internal set => _httpContext = value;
  }

  public IRequestContext? RequestContext => (IRequestContext?) HttpContext?.RequestServices.GetService(typeof(IRequestContext));

  // public override IServiceProvider ServiceProvider => HttpContext?.RequestServices ?? base.ServiceProvider;

  public override string Resource => RequestContext?.Operation?.Type.ToString() ?? (HttpContext?.Request.Method ?? "HTTP");

  public override string? Action => RequestContext?.Operation == null ? HttpContext?.Request.Path.Value : RequestContext.Operation.Name ?? RequestContext.OperationId;
}

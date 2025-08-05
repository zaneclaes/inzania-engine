using System;
using HotChocolate.Execution;
using IZ.Core.Contexts;
using IZ.Core.Data;
using IZ.Core.Observability.Logging;
using IZ.Schema;
using IZ.Schema.Loaders;
using IZ.Server.Requests;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace IZ.Server.Http;

public class StubRequestContextAccessor : IRequestContextAccessor {
  public IRequestContext RequestContext { get; } = null!;
}

public class HttpRootContextAccessor : IProvideRootContext {
  public IZRootContext? GetRootContext(IServiceProvider sp) {
    // var http = sp.GetService<IHttpContextAccessor>()?.HttpContext;
    // if (http == null) return null; // background / work context
    // var scope = http.EnsureRootScope("Start");
    // return scope.Context;
    if (_requestContextAccessor == null || (_requestContextAccessor is StubRequestContextAccessor))
      return sp.GetRequiredService<IZRootContext>();
    try {
      return _requestContextAccessor.RequestContext.Services.GetRequiredService<IZRootContext>();
    } catch {
      // accessing the RequestContext outside a request throws :(
      return sp.GetRequiredService<IZRootContext>();
    }
  }

  // public IZResolver GetResolver(IZContext context) => _requestContextAccessor.RequestContext.Services.GetRequiredService<IZResolver>();

  private readonly IRequestContextAccessor? _requestContextAccessor;
  public HttpRootContextAccessor(IRequestContextAccessor? requestContextAccessor) {
    _requestContextAccessor = requestContextAccessor;
  }
}

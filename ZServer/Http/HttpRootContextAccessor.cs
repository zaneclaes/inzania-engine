using System;
using IZ.Core.Contexts;
using IZ.Core.Data;
using IZ.Core.Observability.Logging;
using IZ.Schema;
using IZ.Schema.Loaders;
using IZ.Server.Requests;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace IZ.Server.Http;

public class HttpRootContextAccessor : IProvideRootContext {
  public IZRootContext? GetRootContext(IServiceProvider sp) {
    // var http = sp.GetService<IHttpContextAccessor>()?.HttpContext;
    // if (http == null) return null; // background / work context
    // var scope = http.EnsureRootScope("Start");
    // return scope.Context;
    return sp.GetRequiredService<IZRootContext>();
  }

  public IZResolver GetResolver(IZContext context) => _resolver ??= new ZSchemaResolver(context);
  private ZSchemaResolver? _resolver;
}

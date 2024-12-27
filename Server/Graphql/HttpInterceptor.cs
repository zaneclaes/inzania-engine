#region

using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using HotChocolate.AspNetCore;
using HotChocolate.Execution;
using IZ.Core.Contexts;
using Microsoft.AspNetCore.Http;

#endregion

namespace IZ.Server.Graphql;

public class HttpInterceptor : DefaultHttpRequestInterceptor {
  public override async ValueTask OnCreateAsync(
    HttpContext context, IRequestExecutor executor, OperationRequestBuilder builder, CancellationToken cancellationToken
  ) {
    var ctxt = context.RequestServices.GetCurrentContext();
    // using var opScope = TraceSpan.FirstLoad(nameof(HttpInterceptor));
    try {
      var identity = await context.GetUserIdentity(ctxt);
      builder.SetGlobalState(nameof(ClaimsPrincipal), identity.Principal);
      context.ClaimTuneIdentity(identity);
      ctxt.Log.Information("[AUTH] session is now {@id}", identity.UserSession);
    } catch (Exception e) {
      ctxt.Log.Error(e, "Auth Error");
    }

    await base.OnCreateAsync(context, executor, builder, cancellationToken);
  }
}



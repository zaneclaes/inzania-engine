#region

using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using HotChocolate.AspNetCore;
using HotChocolate.Execution;
using IZ.Core.Auth;
using IZ.Core.Contexts;
using Microsoft.AspNetCore.Http;

#endregion

namespace IZ.Server.Graphql;

public class ZHttpInterceptor<TAuth> : DefaultHttpRequestInterceptor where TAuth : IZAuthenticator, new() {
  public static Func<IZContext, Task<IZIdentity>> Authenticator = default!;

  public override async ValueTask OnCreateAsync(
    HttpContext http, IRequestExecutor executor, OperationRequestBuilder builder, CancellationToken cancellationToken
  ) {
    var ctxt = http.RequestServices.GetCurrentContext();
    // using var opScope = TraceSpan.FirstLoad(nameof(HttpInterceptor));
    try {
      var auth = new TAuth();
      string? authToken = http.GetAuthToken();
      string? installId = http.GetInstallId();
      var identity = await auth.Authenticate(ctxt, installId, authToken, http.User);
      builder.SetGlobalState(nameof(ClaimsPrincipal), identity.Principal);
      http.ClaimZIdentity(identity);
      ctxt.Log.Information("[AUTH] session is now {@id}", identity.UserSession);
    } catch (Exception e) {
      ctxt.Log.Error(e, "Auth Error");
    }

    await base.OnCreateAsync(http, executor, builder, cancellationToken);
  }
}



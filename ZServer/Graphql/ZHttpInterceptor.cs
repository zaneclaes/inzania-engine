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
using Microsoft.Extensions.DependencyInjection;

#endregion

namespace IZ.Server.Graphql;

public class ZHttpInterceptor : DefaultHttpRequestInterceptor {
  public override async ValueTask OnCreateAsync(
    HttpContext http, IRequestExecutor executor, OperationRequestBuilder builder, CancellationToken cancellationToken
  ) {
    var identity = await http.Authenticate();
    if (identity != null)
      builder.SetGlobalState(nameof(ClaimsPrincipal), identity.Principal);

    await base.OnCreateAsync(http, executor, builder, cancellationToken);
  }

}

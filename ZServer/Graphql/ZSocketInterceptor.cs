#region

using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using HotChocolate.AspNetCore;
using HotChocolate.AspNetCore.Subscriptions;
using HotChocolate.AspNetCore.Subscriptions.Protocols;
using HotChocolate.Execution;
using IZ.Core;
using IZ.Core.Api.GraphQLWebSockets;
using IZ.Core.Auth;
using IZ.Core.Contexts;
using IZ.Core.Json;
using IZ.Server.Requests;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

#endregion

namespace IZ.Server.Graphql;

public class ZSocketInterceptor : DefaultSocketSessionInterceptor {

  public override async ValueTask<ConnectionStatus> OnConnectAsync(
    ISocketSession session,
    IOperationMessagePayload message,
    CancellationToken cancellationToken = default) {
    using var op = session.Connection.HttpContext.AddRequestSpan(typeof(ZSocketInterceptor), nameof(OnConnectAsync));

    var obj = message.As<ZWebSocketConnectionPayload>();
    if (!string.IsNullOrWhiteSpace(obj?.Authorization)) {
      try {
        var ctxt = session.Connection.RequestServices.GetCurrentContext();
        var auth = ctxt.GetRequiredService<IZAuthenticator>();
        var http = session.Connection.HttpContext;
        var authToken = HttpExtensions.GetAuthTokenFromString(obj.Authorization);
        var identity = await auth.Authenticate(ctxt, obj.InstallId, authToken, http.User) ??
                       throw new Exception("No user identity returned");
        // builder.SetGlobalState(nameof(ClaimsPrincipal), identity.Principal);
        http.ClaimZIdentity(identity);
      } catch (Exception e) {
        Log.Error(e, "Failed to authenticate WebSocket");
        ConnectionStatus.Reject();
      }
    } else {
      ConnectionStatus.Reject();
    }
    return await base.OnConnectAsync(session, message, cancellationToken);
  }

  public override async ValueTask OnRequestAsync(
    ISocketSession session,
    string operationSessionId,
    OperationRequestBuilder requestBuilder,
    CancellationToken cancellationToken = default
  ) {
    using var op = session.Connection.HttpContext.AddRequestSpan(typeof(ZSocketInterceptor), nameof(OnRequestAsync));
    await base.OnRequestAsync(session, operationSessionId, requestBuilder, cancellationToken);
  }

  public override async ValueTask OnCloseAsync(
    ISocketSession session,
    CancellationToken cancellationToken = default) {
    using var op = session.Connection.HttpContext.AddRequestSpan(typeof(ZSocketInterceptor), nameof(OnCloseAsync));
    await base.OnCloseAsync(session, cancellationToken);
  }
}

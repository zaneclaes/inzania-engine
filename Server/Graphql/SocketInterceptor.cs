#region

using System.Threading;
using System.Threading.Tasks;
using HotChocolate.AspNetCore;
using HotChocolate.AspNetCore.Subscriptions;
using HotChocolate.AspNetCore.Subscriptions.Protocols;
using HotChocolate.Execution;
using IZ.Server.Requests;

#endregion

namespace IZ.Server.Graphql;

public class SocketInterceptor : DefaultSocketSessionInterceptor {

  public override async ValueTask<ConnectionStatus> OnConnectAsync(
    ISocketSession session,
    IOperationMessagePayload connectionInitMessage,
    CancellationToken cancellationToken = default) {
    // using var op = TraceSpan.Execute(nameof(OnConnectAsync));
    // if (connectionInitMessage.Payload?.TryGetValue("Token", out object? value) ?? false) {
    //   // todo: token auth for sockets
    // }
    // using var op = session.Connection.HttpContext.ApiSpan("WS", nameof(OnConnectAsync));
    using var op = session.Connection.HttpContext.AddRequestSpan(typeof(SocketInterceptor), nameof(OnConnectAsync));
    return await base.OnConnectAsync(session, connectionInitMessage, cancellationToken);
  }

  public override async ValueTask OnRequestAsync(
    ISocketSession session,
    string operationSessionId,
    OperationRequestBuilder requestBuilder,
    CancellationToken cancellationToken = default
  ) {
    using var op = session.Connection.HttpContext.AddRequestSpan(typeof(SocketInterceptor), nameof(OnRequestAsync));
    await base.OnRequestAsync(session, operationSessionId, requestBuilder, cancellationToken);
  }

  public override async ValueTask OnCloseAsync(
    ISocketSession session,
    CancellationToken cancellationToken = default) {
    using var op = session.Connection.HttpContext.AddRequestSpan(typeof(SocketInterceptor), nameof(OnCloseAsync));
    // using var op = session.Connection.HttpContext.ApiSpan("WS", nameof(OnCloseAsync));
    await base.OnCloseAsync(session, cancellationToken);
  }
}

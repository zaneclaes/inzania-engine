using System;

namespace IZ.Core.Api.GraphQLWebSockets;

public interface IGraphQlWebSocket<TData> : IDisposable where TData : class {
  public GqlWebSocketState State { get; }

  public void Update();
}

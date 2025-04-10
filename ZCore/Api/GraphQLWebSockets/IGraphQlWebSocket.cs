using System;

namespace IZ.Core.Api.GraphQLWebSockets;

public interface IGraphQlWebSocket<TData> : IDisposable where TData : class {
  public void Update();
}

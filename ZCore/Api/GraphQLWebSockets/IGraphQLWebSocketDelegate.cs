namespace IZ.Core.Api.GraphQLWebSockets;

public interface IGraphQLWebSocketDelegate<TData> {
  public void OnGraphQLWebSocketData(TData data);

  public void OnGraphQLWebSocketState(GqlWebSocketState state);
}

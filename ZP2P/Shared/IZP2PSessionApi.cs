using System.Collections.Generic;
using System.Threading.Tasks;
using IZ.Core.Api.GraphQLWebSockets;

namespace IZ.P2P.Shared;

public interface IZP2PSessionApi<TSession, TMsg> where TMsg : class {
  public Task<TSession> JoinSession(string key, List<string> connectionStrings);

  public Task<TSession> CreateSession(List<string> connectionStrings);

  public Task<TSession> LoadSessionByKey(string key);

  public Task<IGraphQlWebSocket<TMsg>> SubscribeToSession(string key, IGraphQLWebSocketDelegate<TMsg> webSocketDelegate);
}

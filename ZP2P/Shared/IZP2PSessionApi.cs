using System.Collections.Generic;
using System.Threading.Tasks;
using IZ.Core.Api.GraphQLWebSockets;

namespace IZ.P2P.Shared;

public interface IZP2PSessionApi<TSession, TMsg, TMember> where TMsg : class {
  public TSession? Session { get; }

  public Dictionary<string, TMember> Members { get; }

  public TMember? Self { get; }

  public Task<TSession> JoinSession(string key, List<string> connectionStrings);

  public Task<TSession> CreateSession(List<string> connectionStrings);

  public Task<TSession> LoadSessionByKey(string key);

  public Task<IGraphQlWebSocket<TMsg>> SubscribeToSession(string key, IGraphQLWebSocketDelegate<TMsg> webSocketDelegate);

  public Task<TSession> UpdateGuest(string memberId, bool connected, ushort? ping); // for the host only, update their state

  public void Reset();
}

using System.Collections.Generic;
using System.Threading.Tasks;
using IZ.Core.Api.GraphQLWebSockets;
using IZ.Core.Utils;

namespace IZ.P2P.Shared;

public interface IZP2PSessionApi<TSession, TMsg, TMember> where TMsg : class {
  public TSession? Session { get; }

  public Dictionary<string, TMember> Members { get; }

  public TMember? Self { get; }

  public ZTask<TSession> JoinSession(string key, List<string> connectionStrings);

  public ZTask<TSession> CreateSession(List<string> connectionStrings);

  public ZTask<TSession> LoadSessionByKey(string key);

  public ZTask<IGraphQlWebSocket<TMsg>> SubscribeToSession(string key, IGraphQLWebSocketDelegate<TMsg> webSocketDelegate);

  public ZTask<TSession> UpdateGuest(string memberId, bool connected, ushort? ping); // for the host only, update their state

  public void Reset();
}

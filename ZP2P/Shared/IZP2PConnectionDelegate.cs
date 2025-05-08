using IZ.Core.Contexts;
using IZ.P2P.Data;

namespace IZ.P2P.Shared;

public interface IZP2PConnectionDelegate<TMsg, TPacket, TSession, TMember> : IHaveContext where TMsg : class {
  public IZP2PSessionApi<TSession, TMsg> SessionApi { get; }

  public void OnConnectionState(ZP2PConnectionState state);

  public void OnMemberPing(TMember member, ushort ping);

  public void OnPacket(TMember sender, TPacket packet);

  public void OnMemberConnected(TMember member);

  public void OnMemberUpdated(TMember member);

  public void OnSessionUpdated(TSession session);

  public void OnMemberDisconnected(TMember member);
}

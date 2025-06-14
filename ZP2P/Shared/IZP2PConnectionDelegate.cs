using System.Threading.Tasks;
using IZ.Core.Contexts;
using IZ.P2P.Data;

namespace IZ.P2P.Shared;

public enum P2PCloseReason {
  HostEnded,
  ServerConnectionLost,
  AllMembersLeft,
}

public interface IZP2PConnectionDelegate<TMsg, TPacket, TSession, TMember> : IHaveContext where TMsg : class {
  public IZP2PSessionApi<TSession, TMsg, TMember> SessionApi { get; }

  public Task<ZNetworkInterface> ChooseNetworkInterface();

  public void OnConnectionState(ZP2PConnectionState state);

  public void OnMemberPing(TMember member, ushort ping);

  public void OnPacket(TMember sender, TPacket packet);

  public void OnMemberConnected(TMember member);

  public void OnMemberUpdated(TMember member);

  public Task OnSessionUpdated(TSession session);

  public void OnMemberDisconnected(TMember member);

  public void OnRemoteClosed(P2PCloseReason reason);
}

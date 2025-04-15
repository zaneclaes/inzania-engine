using IZ.Core.Contexts;
using IZ.P2P.Data;

namespace IZ.P2P.Shared;

public interface IZP2PConnectionDelegate : IHaveContext {
  public void OnConnectionState(ZP2PConnectionState state);

  public void OnMemberPing(IZP2PMember member, ushort ping);

  public void OnMemberConnected(IZP2PMember member);

  public void OnMemberDisconnected(IZP2PMember member);
}

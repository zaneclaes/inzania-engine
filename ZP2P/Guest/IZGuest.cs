using System.Threading.Tasks;
using IZ.P2P.Data;
using IZ.P2P.Packets;
using IZ.P2P.Shared;

namespace IZ.P2P.Guest;

public interface IZGuest<TMsg, TPacket, TSession, TMember> : IZP2P<TMsg, TPacket, TSession, TMember>
  where TSession : IZP2PSession<TMember>
  where TMember : IZP2PMember
  where TMsg : IZP2PMessage<TSession, TMember>
  where TPacket : ZPacket {
  // public string? IpAddress { get; }

  // public int? Ping { get; }

  public Task<TSession> Connect(string key, params string[] contentTypes);
}

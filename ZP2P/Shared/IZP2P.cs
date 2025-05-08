using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using IZ.Core.Api.GraphQLWebSockets;
using IZ.P2P.Data;
using IZ.P2P.Packets;

namespace IZ.P2P.Shared;

public interface IZP2P<TMsg, TPacket, TSession, TMember> : IGraphQLWebSocketDelegate<TMsg>, IDisposable
  where TMsg : IZP2PMessage<TSession, TMember>
  where TSession : IZP2PSession<TMember>
  where TMember : IZP2PMember
  where TPacket : ZPacket
{
  // public ushort? PortNumber { get; }

  public bool IsRunning { get; }

  // Who are we?
  public TMember Member { get; }

  public List<TMember> Members { get; }

  public Task SendPacket(TPacket packet, string? memberId = null); // null implies BROADCAST

  public TSession? Session { get; }
}

using IZ.Core.Data;

namespace IZ.P2P.Packets;

public class ZPacket : ApiObject {
  public virtual PacketSendStrategy SendStrategy { get; set; } = PacketSendStrategy.ReliableOrdered;
}

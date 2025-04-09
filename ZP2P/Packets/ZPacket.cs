using IZ.Core.Data;

namespace IZ.P2P.Packets;

public abstract class ZPacket : TransientObject {
  public virtual void Serialize(IZPacketSerializer serializer) {}
  public virtual void Deserialize(IZPacketSerializer serializer) {}
}

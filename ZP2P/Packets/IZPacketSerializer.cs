using System.IO;
using System.Threading.Tasks;
using IZ.Core.Contexts;

namespace IZ.P2P.Packets;

public interface IZPacketSerializer {
  public Task SerializePacketStream<TPacket>(TPacket packet, Stream stream) where TPacket : ZPacket;

  public byte[] SerializePacketData<TPacket>(TPacket packet) where TPacket : ZPacket;

  public Task<TPacket> DeserializePacketStream<TPacket>(IZContext context, Stream stream) where TPacket : ZPacket;

  public TPacket DeserializePacketData<TPacket>(IZContext context, byte[] data) where TPacket : ZPacket;
}

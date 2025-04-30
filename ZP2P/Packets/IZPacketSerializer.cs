using System.IO;
using System.Threading.Tasks;
using IZ.Core.Contexts;

namespace IZ.P2P.Packets;

public interface IZPacketSerializer {
  public Task SerializePacketStream<TPacket>(TPacket packet, Stream stream);

  public byte[] SerializePacketData<TPacket>(TPacket packet);

  public Task<TPacket> DeserializePacketStream<TPacket>(IZContext context, Stream stream);

  public TPacket DeserializePacketData<TPacket>(IZContext context, byte[] data);
}

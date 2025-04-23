using System.IO;
using System.Threading.Tasks;
using IZ.Core.Contexts;

namespace IZ.P2P.Packets;

public interface IZPacketSerializer {
  public Task SerializePacket<TPacket>(TPacket packet, Stream stream);

  public Task<TPacket> DeserializePacket<TPacket>(IZContext context, Stream stream);
}

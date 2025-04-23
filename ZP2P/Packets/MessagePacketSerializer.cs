using System.IO;
using System.Threading.Tasks;
using IZ.Core.Contexts;
using MessagePack;

namespace IZ.P2P.Packets;

public class MessagePacketSerializer : LogicBase, IZPacketSerializer {
  public Task SerializePacket<TPacket>(TPacket packet, Stream stream) =>
    MessagePackSerializer.SerializeAsync<TPacket>(stream, packet);

  public async Task<TPacket> DeserializePacket<TPacket>(IZContext context, Stream stream) {
    var ret = await MessagePackSerializer.DeserializeAsync<TPacket>(stream);
    // ret.Context = context;
    return ret;
  }

  public MessagePacketSerializer(IZContext context) : base(context) {}
}

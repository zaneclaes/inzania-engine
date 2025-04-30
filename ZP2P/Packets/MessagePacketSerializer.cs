using System.IO;
using System.Threading.Tasks;
using IZ.Core.Contexts;
using MessagePack;

namespace IZ.P2P.Packets;

public class MessagePacketSerializer : LogicBase, IZPacketSerializer {
  public Task SerializePacketStream<TPacket>(TPacket packet, Stream stream) =>
    MessagePackSerializer.SerializeAsync<TPacket>(stream, packet);

  public byte[] SerializePacketData<TPacket>(TPacket packet) =>
    MessagePackSerializer.Serialize<TPacket>(packet);

  public async Task<TPacket> DeserializePacketStream<TPacket>(IZContext context, Stream stream) {
    var ret = await MessagePackSerializer.DeserializeAsync<TPacket>(stream);
    // ret.Context = context;
    return ret;
  }

  public TPacket DeserializePacketData<TPacket>(IZContext context, byte[] data) =>
    MessagePackSerializer.Deserialize<TPacket>(data);

  public MessagePacketSerializer(IZContext context) : base(context) {}
}

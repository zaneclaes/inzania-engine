using System;
using System.IO;
using System.Threading.Tasks;
using IZ.Core.Contexts;
using IZ.Core.Data;
using MessagePack;

namespace IZ.P2P.Packets;

public class MessagePacketSerializer : LogicBase, IZPacketSerializer {
  private readonly MessagePackSerializerOptions _options;

  public MessagePacketSerializer(IZContext context) : base(context) {
    _options = MessagePackSerializer.DefaultOptions.WithZResolver(Context);
  }

  public Task SerializePacketStream<TPacket>(TPacket packet, Stream stream) where TPacket : ZPacket =>
    MessagePackSerializer.SerializeAsync<ZPacket>(stream, packet, _options);

  public byte[] SerializePacketData<TPacket>(TPacket packet) where TPacket : ZPacket =>
    MessagePackSerializer.Serialize<ZPacket>(packet, _options);

  public byte[] SerializeApiData<TPacket>(TPacket packet) where TPacket : ApiObject =>
    MessagePackSerializer.Serialize<ApiObject>(packet, _options);

  public async Task<TPacket> DeserializePacketStream<TPacket>(IZContext context, Stream stream) where TPacket : ZPacket {
    var ret = await MessagePackSerializer.DeserializeAsync<ZPacket>(stream, _options);
    ret.Context = context;
    return ret as TPacket ?? throw new SystemException($"Could not convert packet {ret.GetType()} to {typeof(TPacket)}");
  }

  public TPacket DeserializePacketData<TPacket>(IZContext context, byte[] data) where TPacket : ZPacket {
    var ret = MessagePackSerializer.Deserialize<ZPacket>(data, _options);
    ret.Context = context;
    return ret as TPacket ?? throw new SystemException($"Could not convert packet {ret.GetType()} to {typeof(TPacket)}");
  }

  public TPacket DeserializeApiData<TPacket>(IZContext context, byte[] data) where TPacket : ApiObject {
    var ret = MessagePackSerializer.Deserialize<ZPacket>(data, _options);
    ret.Context = context;
    return ret as TPacket ?? throw new SystemException($"Could not convert packet {ret.GetType()} to {typeof(TPacket)}");
  }
}

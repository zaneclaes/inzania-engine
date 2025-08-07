using IZ.Core.Contexts;
using MessagePack;
using MessagePack.Formatters;
using MessagePack.Resolvers;

namespace IZ.P2P.Packets;

public static class MessagePackExtensions {
  public static MessagePackSerializerOptions WithZResolver(this MessagePackSerializerOptions opts, IZContext? context = null) =>
    opts.WithResolver(CompositeResolver.Create(
      new IMessagePackFormatter[] {
        new ZPacketFormatter(context)
      },
      new IFormatterResolver[] {
        StandardResolver.Instance
      }));
}

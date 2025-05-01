using System;

namespace IZ.Core.Data.Attributes;

[AttributeUsage(AttributeTargets.Class)]
public sealed class ApiPacketAttribute : Attribute {
  public byte PacketDiscriminator { get; }

  public ApiPacketAttribute(byte packetDiscriminator) {
    PacketDiscriminator = packetDiscriminator;
  }
}

using System;

namespace IZ.Core.Data.Attributes;

[AttributeUsage(AttributeTargets.Class)]
public sealed class ApiPacketAttribute : Attribute {

  public ApiPacketAttribute(byte packetDiscriminator) {
    PacketDiscriminator = packetDiscriminator;
  }
  public byte PacketDiscriminator { get; }
}

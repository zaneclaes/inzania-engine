using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using IZ.Core.Api;
using IZ.Core.Api.Types;
using IZ.Core.Contexts;
using MessagePack;
using MessagePack.Formatters;
using MessagePack.Resolvers;

namespace IZ.P2P.Packets;

public class ZPacketFormatter : LogicBase, IMessagePackFormatter<ZPacket?> {
  private readonly Dictionary<byte, List<ZTypeDescriptor>> _packetTypes;

  private Tuple<List<int>, Dictionary<int, List<ZPropertyDescriptor>>> GetOrders(ZTypeDescriptor desc) {
    var orderGroups = desc.ObjectDescriptor.AllProperties
      .Where(p => p.Order >= 0)
      .GroupBy(p => p.Order)
      .ToDictionary(p => p.Key, p => p.ToList());
    if (!orderGroups.Any()) Log.Warning("[PACKET] {type} has no ordered properties", desc);
    var orders = orderGroups.Keys.ToList();
    orders.Sort();
    return new Tuple<List<int>, Dictionary<int, List<ZPropertyDescriptor>>>(orders, orderGroups);
  }

  public void Serialize(ref MessagePackWriter writer, ZPacket? value, MessagePackSerializerOptions options) {
    if (value == null) return;
    var desc = ZTypeDescriptor.FromType(value.GetType());
    if (desc.ObjectDescriptor.PacketDiscriminator <= 0) throw new ArgumentException($"[PACKET] {desc} has no packet discriminator");
    writer.WriteUInt8(desc.ObjectDescriptor.PacketDiscriminator);

    var (orders, orderGroups) = GetOrders(desc);
    foreach (var order in orders) {
      var group = orderGroups[order];
      if (group.Count > 1) Log.Warning("[PACKET] {type} has {count} entries for {order}", desc, group.Count, order);
      foreach (var prop in group) {
        var val = prop.GetValue(value);
        if (prop.FieldType == typeof(string)) {
          var sval = val as string;
          writer.Write(sval);
          // writer.WriteStringHeader(sval?.Length ?? 0);
          // if (sval != null) writer.WriteString(Encoding.UTF8.GetBytes(sval));
        } else if (prop.FieldType == typeof(byte[])) {
          var sval = val as byte[];
          writer.WriteBinHeader(sval?.Length ?? 0);
          // writer.WriteStringHeader(sval?.Length ?? 0);
          if (sval != null) writer.WriteRaw(sval);
        } else if (prop.FieldType.IsEnum) {
          var ut = Enum.GetUnderlyingType(prop.FieldType);
          if (!WriteValue(ref writer, ut, val)) Log.Error("[PACKET] ENUM {type} = {val} ({valType}) failed to write", prop.FieldType, val, ut);
        } else if (!WriteValue(ref writer, prop.FieldType, val)) {
          Log.Error("[PACKET] unknown packet field type for {type}.{field}", desc, prop);
        }
      }
    }
  }

  private bool WriteValue(ref MessagePackWriter writer, Type type,object? val) {
    var nullableType = Nullable.GetUnderlyingType(type);
    if (nullableType != null) {
      if (val == null) {
        writer.WriteNil();
        return true;
      }
      return WriteValue(ref writer, nullableType, val);
    }

    if (type == typeof(byte)) writer.WriteUInt8((byte)(val ?? 0));
    else if (type == typeof(ushort)) writer.WriteUInt16((ushort)(val ?? 0));
    else if (type == typeof(uint)) writer.WriteUInt32((uint)(val ?? 0));
    else if (type == typeof(ulong)) writer.WriteUInt64((ulong)(val ?? 0));
    else if (type == typeof(sbyte)) writer.WriteInt8((sbyte)(val ?? 0));
    else if (type == typeof(short)) writer.WriteInt16((short)(val ?? 0));
    else if (type == typeof(int)) writer.WriteInt32((int)(val ?? 0));
    else if (type == typeof(long)) writer.WriteInt64((long) (val ?? 0));
    else return false;
    return true;
  }

  private object? ReadValue(ref MessagePackReader reader, Type type) {
    var nullableType = Nullable.GetUnderlyingType(type);
    if (nullableType != null) {
      if (reader.TryReadNil()) return null;
      return ReadValue(ref reader, nullableType);
    }

    if (type == typeof(byte)) return reader.ReadByte();
    else if (type == typeof(ushort)) return reader.ReadUInt16();
    else if (type == typeof(uint)) return reader.ReadUInt32();
    else if (type == typeof(ulong)) return reader.ReadUInt64();
    else if (type == typeof(sbyte)) return reader.ReadSByte();
    else if (type == typeof(short)) return reader.ReadInt16();
    else if (type == typeof(int)) return reader.ReadInt32();
    else if (type == typeof(long)) return reader.ReadInt64();
    else throw new ArgumentException($"ZPacket had invalid scalar type {type}");
  }

  public ZPacket Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options) {
    options.Security.DepthStep(ref reader);
    try {
      var discrim = reader.ReadByte();
      if (!_packetTypes.TryGetValue(discrim, out var packetTypes) || packetTypes.Count == 0) {
        throw new ArgumentException($"ZPacket discriminator {discrim} is not supported");
      }
      var desc = _packetTypes[discrim].First();
      var packetType = desc.ObjectDescriptor.ObjectType;
      var pack = Activator.CreateInstance(packetType) ?? throw new ArgumentException($"Failed to create instance of {packetType.Name}");
      var packet = pack as ZPacket ?? throw new ArgumentException($"Failed to convert {packetType.Name} into ZPacket");
      var (orders, orderGroups) = GetOrders(desc);
      foreach (var order in orders) {
        var group = orderGroups[order];
        if (group.Count > 1) Log.Warning("[PACKET] {type} has {count} entries for {order}", desc, group.Count, order);
        foreach (var prop in group) {
          object? val = null;
          if (prop.FieldType == typeof(string)) {
            var seq = reader.ReadStringSequence() ?? throw new ArgumentException($"NULL sequence for {desc}.{prop}");
            val = Encoding.UTF8.GetString(seq.ToArray());
          } else if (prop.FieldType == typeof(byte[])) {
            val = reader.ReadBytes()?.ToArray() ?? throw new ArgumentException($"NULL sequence for {desc}.{prop}");
          } else if (prop.FieldType.IsEnum) {
            val = ReadValue(ref reader, Enum.GetUnderlyingType(prop.FieldType));
          } else {
            val = ReadValue(ref reader, prop.FieldType);
          }
          prop.SetValue(packet, val);
          Log.Verbose("[PACKET] {p} = {val}", prop.Name, val);;
        }
      }
      return packet;
    } finally {
      reader.Depth--;
    }
  }

  public ZPacketFormatter(IZContext? context) : base(context) {
    var types = ZTypeDescriptor.ApiTypes.Values.ToList();
    _packetTypes = types.Where(t => t.ObjectDescriptor.PacketDiscriminator > 0)
      .GroupBy(t => t.ObjectDescriptor.PacketDiscriminator)
      .ToDictionary(p => p.Key, p => p.ToList());
    if (!_packetTypes.Any()) Log.Error("[PACKET] no classes have the ApiPacket attribute among {names}", types.Select(t => t.ToString()));
    foreach (var type in _packetTypes.Keys) {
      if (_packetTypes[type].Count != 1) Log.Error("[PACKET] packet discriminator {disc} is assigned to: {types}", type, _packetTypes[type].Select(t => t.ToString()));
    }
  }
  public ZPacketFormatter() : this(null) { }
}

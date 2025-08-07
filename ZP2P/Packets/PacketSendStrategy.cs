namespace IZ.P2P.Packets;

// Copied from LiteNetLib DeliveryMethod! Gets directly converted.
public enum PacketSendStrategy {
  /// <summary>
  ///   Unreliable. Packets can be dropped, can be duplicated, can arrive without order.
  /// </summary>
  Unreliable = 4,

  /// <summary>
  ///   Reliable. Packets won't be dropped, won't be duplicated, can arrive without order.
  /// </summary>
  ReliableUnordered = 0,

  /// <summary>
  ///   Unreliable. Packets can be dropped, won't be duplicated, will arrive in order.
  /// </summary>
  Sequenced = 1,

  /// <summary>
  ///   Reliable and ordered. Packets won't be dropped, won't be duplicated, will arrive in order.
  /// </summary>
  ReliableOrdered = 2,

  /// <summary>
  ///   Reliable only last packet. Packets can be dropped (except the last one), won't be duplicated, will arrive in order.
  ///   Cannot be fragmented
  /// </summary>
  ReliableSequenced = 3
}

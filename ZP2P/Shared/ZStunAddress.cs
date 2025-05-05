using IZ.Core.Data;
using IZ.P2P.Data;

namespace IZ.P2P.Shared;

public class ZStunAddress : TransientObject {
  public string IpAddress { get; set; } = null!;

  public int BindPort { get; set; }

  public int PublicPort { get; set; }

  public ZP2PAccessibility Accessibility { get; set; }

  public override string ToString() => $"{Accessibility}:{IpAddress}:{BindPort}:{PublicPort}";
}

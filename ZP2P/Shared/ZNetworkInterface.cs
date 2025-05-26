using System.Net;
using IZ.Core.Contexts;
using IZ.P2P.Data;

namespace IZ.P2P.Shared;

// Represents a physical NIC, with IPv4 and/or IPv6
public class ZNetworkInterface {
  public string InterfaceName { get; set; } = null!;

  // LAN address
  public IPAddress PrivateIPv4 { get; set; } = null!;

  // Public STUN-discovered address for the IPv4
  public IPEndPoint? PublicIPv4 { get; set; }

  // When using IPv4, the port which was used to get the public STUN endpoint
  public int BindPort { get; set; }

  // GLOBAL IPv6 works both privately and publicly
  public IPAddress? GlobalIPv6 { get; set; } = null!;

  public bool IsValid => PublicIPv4 != null || GlobalIPv6 != null;

  public List<string> GetConnectionOptions(string? contentType = null) {
    var options = new List<string>();
    options.Add(CreateConnectionString(PrivateIPv4, BindPort, ZP2PAccessibility.Local, contentType));
    if (PublicIPv4 != null)
      options.Add(CreateConnectionString(PublicIPv4.Address, PublicIPv4.Port, ZP2PAccessibility.Public, contentType));
    if (GlobalIPv6 != null)
      options.Add(CreateConnectionString(GlobalIPv6, 0, ZP2PAccessibility.Public, contentType));
    return options;
  }

  private string CreateConnectionString(IPAddress ip, int port, ZP2PAccessibility accessibility, string? contentType = null) =>
    $"{ip}|{port}|{accessibility}" + (contentType != null ? $"|{contentType}" : "");

  public override string ToString() => $"<NIC {InterfaceName} v4={PrivateIPv4} public={PublicIPv4} v6={GlobalIPv6} />";

  private const int PublicPortStart = 25678; // 50000;

  private const int MaxPortNumber = 40000; // 65535;

  public static async Task<List<ZNetworkInterface>> Discover(IZContext context, int portOffset = 0) {
    using var stunClient = new ZStunClient(context);
    // var portOffset = new Random().Next(0, MaxPortNumber - PublicPortStart - 100); // avoid port collisions wherever possible
    var addresses = await stunClient.GetInterfaceAddresses(PublicPortStart + portOffset);
    if (!addresses.Any()) throw new SystemException("No internet connection found!");
    return addresses;
  }

  public static async Task<ZNetworkInterface> Select(IZContext context, int portOffset = 0) {
    var addresses = await Discover(context, portOffset);
    var choice =
      addresses.FirstOrDefault(a => a.GlobalIPv6 != null && a.PublicIPv4 != null) ??
      addresses.FirstOrDefault(a => a.PublicIPv4 != null) ??
      addresses.FirstOrDefault(a => a.GlobalIPv6 != null) ??
      addresses.First();
    if (addresses.Count > 1) context.Log.Information("[NIC] chose {choice} from [{options}]", choice, string.Join(", ", addresses));
    return choice;
  }
}

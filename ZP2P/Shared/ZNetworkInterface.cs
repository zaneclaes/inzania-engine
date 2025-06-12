using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading.Tasks;
using IZ.Core.Contexts;
using IZ.Core.Data;
using IZ.P2P.Data;

namespace IZ.P2P.Shared;

[Flags]
public enum NetworkInterfaceAccessibility {
  PrivateIPv4     = 1 << 0,
  PublicIPv4CgNat = 1 << 1,
  GlobalIPv6      = 1 << 2,
  PublicIPv4      = 1 << 3,
}

// Represents a physical NIC, with IPv4 and/or IPv6
public class ZNetworkInterface : TransientObject {
  public string InterfaceName { get; private set; }

  public NetworkInterfaceType InterfaceType { get; private set; }

  // LAN address
  public IPAddress? PrivateIPv4 { get; private set; }

  // Public STUN-discovered address for the IPv4
  public IPEndPoint? PublicIPv4 { get; set; }

  // When using IPv4, the port which was used to get the public STUN endpoint
  public int BindPort { get; set; }

  // GLOBAL IPv6 works both privately and publicly
  public List<IPAddress> GlobalIPv6 { get; private set; }

  public IPAddress ListenIPv4 => PrivateIPv4 ?? IPAddress.Any;
  public IPAddress ListenIPv6 => !GlobalIPv6.Any() ? IPAddress.IPv6None : (GlobalIPv6.Count > 1 ? IPAddress.IPv6Any : GlobalIPv6.First());

  // Port translation is done by the carrier (carrier grade NAT)
  public bool IsCgNat => PublicIPv4 != null && PublicIPv4.Port != BindPort;

  public NetworkInterfaceAccessibility Accessibility {
    get {
      NetworkInterfaceAccessibility accessibility = 0;
      if (PrivateIPv4 != null) accessibility |= NetworkInterfaceAccessibility.PrivateIPv4;
      if (PublicIPv4 != null) {
        if (IsCgNat)  accessibility |= NetworkInterfaceAccessibility.PublicIPv4CgNat;
        else accessibility |= NetworkInterfaceAccessibility.PublicIPv4;
      }
      if (GlobalIPv6.Any()) accessibility |= NetworkInterfaceAccessibility.GlobalIPv6;
      return accessibility;
    }
  }

  public int Priority => (int) Accessibility;

  public List<string> GetConnectionOptions(string? contentType = null) {
    var options = new List<string>();
    if (PrivateIPv4 != null)
      options.Add(CreateConnectionString(PrivateIPv4, BindPort, ZP2PAccessibility.Local, contentType));
    if (PublicIPv4 != null)
      options.Add(CreateConnectionString(PublicIPv4.Address, PublicIPv4.Port, ZP2PAccessibility.Public, contentType));
    foreach (var ipv6 in GlobalIPv6)
      options.Add(CreateConnectionString(ipv6, BindPort, ZP2PAccessibility.Public, contentType));
    return options;
  }

  private string CreateConnectionString(IPAddress ip, int port, ZP2PAccessibility accessibility, string? contentType = null) =>
    $"{ip}|{port}|{accessibility}" + (contentType != null ? $"|{contentType}" : "");

  public override string ToString() => $"<NIC {InterfaceName} {InterfaceType} {(IsCgNat ? "CGNAT " : "")}" +
                                       $"v4={PrivateIPv4}:{BindPort} public={PublicIPv4} v6=[{string.Join(", ", GlobalIPv6.Select(ip => ip.ToString()))}] />";

  private const int PublicPortStart = 25678; // 50000;

  private const int MaxPortNumber = 40000; // 65535;

  private ZNetworkInterface(IZContext context, string name, NetworkInterfaceType type, IPAddress? privateIPv4, params IPAddress[] globalIPv6) : base(context) {
    InterfaceName = name;
    InterfaceType = type;
    PrivateIPv4 = privateIPv4;
    GlobalIPv6 = globalIPv6.ToList();
  }

  public static List<ZNetworkInterface> AllInterfaces { get; private set; } = new List<ZNetworkInterface>();

  public static async Task<List<ZNetworkInterface>> Discover(IZContext context, int portOffset = 0) {
    using var stunClient = new ZStunClient(context);
    return (AllInterfaces = await stunClient.GetInterfaceAddresses(GetLocalInterfaces(context), PublicPortStart + portOffset)).ToList();
  }

  public static async Task<ZNetworkInterface> Select(IZContext context, int portOffset = 0) {
    var addresses = await Discover(context, portOffset);
    if (!addresses.Any()) throw new SystemException("No internet connection found!");
    addresses.Sort((a, b) => b.Priority.CompareTo(a.Priority));
    var choice = addresses.First();
    context.Log.Information("[NIC] chose {choice} from:\n{options}", choice, string.Join("\n", addresses));
    return choice;
  }

  private static List<ZNetworkInterface> GetLocalInterfaces(IZContext ctx) {
    List<ZNetworkInterface> results = new List<ZNetworkInterface>();

    foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces()) {
      if (ni.OperationalStatus != OperationalStatus.Up)
        continue;

      var props = ni.GetIPProperties();
      IPAddress? ipv4 = null;
      List<IPAddress> ipv6 = new  List<IPAddress>();

      foreach (var ipInfo in props.UnicastAddresses) {
        if (IPAddress.IsLoopback(ipInfo.Address))
          continue;

        if (ipInfo.Address.AddressFamily == AddressFamily.InterNetwork && !IsCgnat(ipInfo.Address) && ipInfo.Address.ToString() != "127.0.0.1") {
          ipv4 = ipInfo.Address;
        } else if (ipInfo.Address.AddressFamily == AddressFamily.InterNetworkV6) {
          if (IsGlobalIPv6(ipInfo.Address)) ipv6.Add(ipInfo.Address);
        }
      }
      if (!ipv6.Any() && ipv4 == null) continue;
      results.Add(new ZNetworkInterface(ctx, ni.Name, ni.NetworkInterfaceType, ipv4, ipv6.ToArray()));
    }
    return results;
  }

  private static bool IsGlobalIPv6(IPAddress ip) => !ip.IsIPv6LinkLocal &&
                                                    !ip.IsIPv6Multicast &&
                                                    !ip.IsIPv6SiteLocal &&
                                                    !IsUniqueLocalIPv6(ip);

  private static bool IsUniqueLocalIPv6(IPAddress ip) {
    var bytes = ip.GetAddressBytes();
    return (bytes[0] & 0xFE) == 0xFC; // fc00::/7
  }

  private static bool IsCgnat(IPAddress ip) =>
    ip.AddressFamily == AddressFamily.InterNetwork &&
    ip.GetAddressBytes()[0] == 100 &&
    (ip.GetAddressBytes()[1] >= 64 && ip.GetAddressBytes()[1] <= 127);
}

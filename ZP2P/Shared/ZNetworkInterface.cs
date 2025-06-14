using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading.Tasks;
using IZ.Core;
using IZ.Core.Contexts;
using IZ.Core.Data;
using IZ.Core.Utils;
using IZ.P2P.Data;
using Lib.Utils;

namespace IZ.P2P.Shared;

[Flags]
public enum NetworkInterfaceAccessibility {
  Ethernet        = 1 << 0, // Slight boost for Ethernet > WiFi
  PrivateIPv4     = 1 << 1,
  GlobalIPv6      = 1 << 3, // A nice fallback, but usually CGNAT is better than IPv6
  PublicIPv4CgNat = 1 << 5,
  PublicIPv4      = 1 << 10, // PublicIPv4 trumps all...
}

// Represents a physical NIC, with IPv4 and/or IPv6
public class ZNetworkInterface : TransientObject {
  public string InterfaceName { get; private set; }

  public ZNetworkInterfaceType InterfaceType { get; private set; }

  // LAN address
  public IPAddress? PrivateIPv4 { get; private set; }

  // Public STUN-discovered address for the IPv4
  public IPEndPoint? PublicIPv4 { get; set; }

  // When using IPv4, the port which was used to get the public STUN endpoint
  public int BindPort { get; set; }

  // GLOBAL IPv6 works both privately and publicly
  public List<IPAddress> GlobalIPv6 { get; private set; }

  public IPAddress ListenIPv4 => PrivateIPv4 ?? IPAddress.Any;
  public IPAddress ListenIPv6 => GlobalIPv6.Count == 1 ? GlobalIPv6[0] : IPAddress.IPv6Any;
    // !GlobalIPv6.Any() ? IPAddress.IPv6None : (GlobalIPv6.Count > 1 ? IPAddress.IPv6Any : GlobalIPv6.First());

  // Port translation is done by the carrier (carrier grade NAT)
  public bool IsCgNat => PublicIPv4 != null && PublicIPv4.Port != BindPort;

  public NetworkInterfaceAccessibility Accessibility {
    get {
      NetworkInterfaceAccessibility accessibility = 0;
      if (InterfaceType == ZNetworkInterfaceType.Ethernet) accessibility |= NetworkInterfaceAccessibility.Ethernet;
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

  private List<string> GetFlags() {
    List<string> ret = new List<string>();
    var flags = Accessibility;
    // if (flags.HasFlag(NetworkInterfaceAccessibility.PrivateIPv4)) ret.Add("private");
    if (flags.HasFlag(NetworkInterfaceAccessibility.PublicIPv4CgNat)) ret.Add("CGNAT");
    if (flags.HasFlag(NetworkInterfaceAccessibility.PublicIPv4)) ret.Add("IPv4");
    if (flags.HasFlag(NetworkInterfaceAccessibility.GlobalIPv6)) ret.Add("IPv6");
    return ret;
  }

  public override string ToString() => $"<NIC {InterfaceName} {InterfaceType} flags=[{string.Join(", ", GetFlags())}] p={Priority} " +
                                       $"v4={PrivateIPv4}:{BindPort} public={PublicIPv4} v6=[{string.Join(", ", GlobalIPv6.Select(ip => ip.ToString()))}] />";

  private const int PublicPortStart = 25678; // 50000;

  private const int MaxPortNumber = 40000; // 65535;

  public ZNetworkInterface(IZContext context, string name, ZNetworkInterfaceType type, IPAddress? privateIPv4, params IPAddress[] globalIPv6) : base(context) {
    InterfaceName = name;
    InterfaceType = type;
    PrivateIPv4 = privateIPv4;
    GlobalIPv6 = globalIPv6.ToList();
  }

  private static List<ZNetworkInterface> AllInterfaces { get; set; } = new List<ZNetworkInterface>();

  public static async Task<List<ZNetworkInterface>> Discover(IZContext context, int portOffset = 0, Func<NetworkInterface, IPAddress?, List<IPAddress>, ZNetworkInterface>? creator = null) {
    using var stunClient = new ZStunClient(context);
    AllInterfaces = await stunClient.GetInterfaceAddresses(GetLocalInterfaces(context, creator), PublicPortStart + portOffset);
    AllInterfaces.Sort((a, b) => b.Priority.CompareTo(a.Priority));
    return AllInterfaces;
  }

  public static async Task<ZNetworkInterface> Select(IZContext context, int portOffset = 0, Func<NetworkInterface, IPAddress?, List<IPAddress>, ZNetworkInterface>? creator = null) {
    var addresses = await Discover(context, portOffset, creator);
    if (!addresses.Any()) throw new SystemException("No internet connection found!");
    var choice = addresses.First();
    context.Log.Information("[NIC] chose {choice} from:\n{options}", choice, string.Join("\n", addresses));
    return choice;
  }

  public static ZNetworkInterfaceType GuessInterfaceType(NetworkInterface nic) {
    if (nic.NetworkInterfaceType == NetworkInterfaceType.Wireless80211) return ZNetworkInterfaceType.WiFi;
    if (nic.NetworkInterfaceType == NetworkInterfaceType.Wman || nic.NetworkInterfaceType == NetworkInterfaceType.Wwanpp || nic.NetworkInterfaceType == NetworkInterfaceType.Wwanpp2)
      return ZNetworkInterfaceType.Cellular;

// #if UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX || UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX
    if (nic.Name == "en0" || nic.Description.ToLower().Contains("airport")) return ZNetworkInterfaceType.WiFi; // Linux + Mac
// #elif UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
    if (nic.Description.ToLower().Contains("wi-fi") || nic.Description.ToLower().Contains("wireless")) return ZNetworkInterfaceType.WiFi; // Windows

    if (nic.Name.StartsWith("wlan"))  return ZNetworkInterfaceType.WiFi; // Android
    if (nic.Name.StartsWith("rmnet") || nic.Name.StartsWith("ccmni") || nic.Name.StartsWith("pdp")) return ZNetworkInterfaceType.Cellular; // Android + iOS
    if (nic.Name.StartsWith("tun") || nic.Name.StartsWith("ipsec"))  return ZNetworkInterfaceType.Vpn;
// #endif

    return ZNetworkInterfaceType.Ethernet;
  }

  // Store the last (ipv4) local addresses to detect network changes...
  private static readonly List<IPAddress> _lastIpAddresses = new List<IPAddress>();

  public static bool HaveInterfacesChanged =>
    !_lastIpAddresses.IsSameSet(GetAvailableInterfaces().Select(ni => GetIpAddresses(ni).Item1).Where(v => v != null).ToList());

  private static List<NetworkInterface> GetAvailableInterfaces() =>
    NetworkInterface.GetAllNetworkInterfaces().Where(ni => ni.OperationalStatus == OperationalStatus.Up).ToList();

  private static Tuple<IPAddress?, List<IPAddress>> GetIpAddresses(NetworkInterface ni) {
    var props = ni.GetIPProperties();
    IPAddress? ipv4 = null;
    List<IPAddress> ipv6 = new List<IPAddress>();
    foreach (var ipInfo in props.UnicastAddresses) {
      if (IPAddress.IsLoopback(ipInfo.Address))
        continue;

      if (IsPublicIPv4(ipInfo.Address)) {
        ipv4 = ipInfo.Address;
      } else if (IsGlobalIPv6(ipInfo.Address)) {
        ipv6.Add(ipInfo.Address);
      }
    }
    return new Tuple<IPAddress, List<IPAddress>>(ipv4, ipv6);
  }

  private static List<ZNetworkInterface> GetLocalInterfaces(IZContext ctx, Func<NetworkInterface, IPAddress?, List<IPAddress>, ZNetworkInterface>? creator) {
    List<ZNetworkInterface> results = new List<ZNetworkInterface>();
    var interfaces = GetAvailableInterfaces();
    _lastIpAddresses.Clear();

    foreach (NetworkInterface ni in interfaces) {
      var (ipv4, ipv6) = GetIpAddresses(ni);
      if (ipv4 != null) _lastIpAddresses.Add(ipv4);
      if (!ipv6.Any() && ipv4 == null) continue;
      results.Add(creator == null ? new ZNetworkInterface(ctx, ni.Name, GuessInterfaceType(ni), ipv4, ipv6.ToArray()) : creator(ni, ipv4, ipv6));
    }
    return results;
  }

  private static bool IsPublicIPv4(IPAddress ip) =>
    ip.AddressFamily == AddressFamily.InterNetwork &&
    !IsCgnat(ip) &&
    ip.ToString() != "127.0.0.1";

  private static bool IsGlobalIPv6(IPAddress ip) =>
    ip.AddressFamily == AddressFamily.InterNetworkV6 &&
    !ip.IsIPv6LinkLocal &&
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

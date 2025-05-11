using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using IZ.Core.Contexts;
using IZ.Core.Data;
using IZ.P2P.Data;
using IZ.P2P.Shared;

namespace IZ.P2P.Shared;

public class ZConnectionOptionSet : TransientObject {
  // PublicIPs come from STUN, and include the public-facing port so that clients can connect to that
  public List<IPEndPoint> PublicIps { get; private set; }

  // LocalIPs don't have a port, because they can be connected to via the ListenPort
  public List<IPAddress> LocalIps { get; private set; }

  public int ListenPort { get; set; }

  // Nearby to the legacy 26000 Quake port, which is widely supported by routers
  private const int PublicPortStart = 25678; // 50000;

  private const int MaxPortNumber = 40000; // 65535;

  public List<string> GetConnectionOptions(string? contentType) =>
    PublicIps.Select(ip => CreateConnectionString(ip.Address, ip.Port, ZP2PAccessibility.Public, contentType))
      .Union(LocalIps.Select(ip => CreateConnectionString(ip, ListenPort, ZP2PAccessibility.Local, contentType)))
      .ToList();

  private string CreateConnectionString(IPAddress ip, int port, ZP2PAccessibility accessibility, string? contentType = null) =>
    $"{ip}|{port}|{accessibility}" + (contentType != null ? $"|{contentType}" : "");

  private ZConnectionOptionSet(IZContext context, int port, List<IPEndPoint> publicIps, List<IPAddress> localIps) : base(context) {
    ListenPort = port;
    PublicIps = publicIps;
    LocalIps = localIps;
  }

  public static async Task<ZConnectionOptionSet> Create(IZContext context, int portOffset = 0) {
    using var stunClient = new ZStunClient(context);
    // var portOffset = new Random().Next(0, MaxPortNumber - PublicPortStart - 100); // avoid port collisions wherever possible
    var (port, publicIps) = await stunClient.GetConnectionOptions(PublicPortStart + portOffset);
    var privateIps = stunClient.GetLocalIpAddresses().Distinct().ToList();
    if (!publicIps.Any() && !privateIps.Any()) throw new SystemException("No IP addresses found!");
    return new ZConnectionOptionSet(context, port, publicIps, privateIps);
  }

  public override string ToString() => $"<IPs Public={string.Join(", ", PublicIps)} Private={string.Join(", ", LocalIps)} Port={ListenPort} />";
}

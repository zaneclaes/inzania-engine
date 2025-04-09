using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using IZ.Core.Contexts;
using IZ.Core.Data;
using IZ.P2P.Data;
using IZ.P2P.Shared;

namespace IZ.P2P.Shared;

public class ZConnectionOptionSet : TransientObject {
  public List<string> PublicIps { get; private set; }

  public List<string> LocalIps { get; private set; }

  public List<string> GetConnectionOptions(ushort port, string? contentType) =>
    PublicIps.Select(ip => CreateConnectionString(ip, port, ZP2PAccessibility.Public, contentType))
      .Union(LocalIps.Select(ip => CreateConnectionString(ip, port, ZP2PAccessibility.Local, contentType)))
      .ToList();

  private string CreateConnectionString(string ip, ushort port, ZP2PAccessibility accessibility, string? contentType = null) =>
    $"{ip}|{port}|{accessibility}" + (contentType != null ? $"|{contentType}" : "");

  private ZConnectionOptionSet(IZContext context, List<string> publicIps, List<string> localIps) : base(context) {
    PublicIps = publicIps;
    LocalIps = localIps;
  }

  public static async Task<ZConnectionOptionSet> Create(IZContext context) {
    using var stunClient = new ZStunClient(context);
    var endPoints = await stunClient.GetConnectionOptions();
    var publicIps = endPoints.Select(ep => ep.Address.ToString()).Distinct().ToList();
    var privateIps = stunClient.GetLocalIpAddresses().Select(ip => ip.ToString()).Distinct().ToList();
    return new ZConnectionOptionSet(context, publicIps, privateIps);
  }
}

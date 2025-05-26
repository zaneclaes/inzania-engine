using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading.Tasks;
using IZ.Core.Contexts;
using IZ.P2P.Data;

namespace IZ.P2P.Shared;

public class ZStunClient : LogicBase {
  public uint StunPort { get; private set; }

  public int Timeout { get; private set; }

  private readonly List<string> _stunServers = new List<string>() {
    "stun.l.google.com",
    "stun1.l.google.com",
  };

  public ZStunClient(IZContext ctx, uint stunPort = 19302, int timeout = 2000) : base(ctx) {
    StunPort = stunPort;
    Timeout = timeout;
  }

  private List<ZNetworkInterface> GetLocalIpAddresses() {
    List<ZNetworkInterface> results = new List<ZNetworkInterface>();

    foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces()) {
      if (ni.OperationalStatus != OperationalStatus.Up)
        continue;

      var props = ni.GetIPProperties();
      IPAddress ipv4 = null;
      IPAddress ipv6 = null;

      foreach (var ipInfo in props.UnicastAddresses) {
        if (IPAddress.IsLoopback(ipInfo.Address))
          continue;

        if (ipInfo.Address.AddressFamily == AddressFamily.InterNetwork && !IsCgnat(ipInfo.Address) && ipInfo.Address.ToString() != "127.0.0.1") {
          ipv4 = ipInfo.Address;
        } else if (ipInfo.Address.AddressFamily == AddressFamily.InterNetworkV6) {
          if (IsGlobalIPv6(ipInfo.Address)) ipv6 = ipInfo.Address;
        }
      }

      if (ipv4 != null || ipv6 != null) {
        if (ipv4 == null) {
          Log.Warning("[STUN] got IPv6 {addr} for {name}, but no IPv4!", ipv6, ni.Name);
          continue;
        }
        results.Add(new ZNetworkInterface {
          InterfaceName = ni.Name,
          PrivateIPv4 = ipv4,
          GlobalIPv6 = ipv6
        });
      }
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

  public async Task<List<ZNetworkInterface>> GetInterfaceAddresses(int port = 0) {
    var addrs = GetLocalIpAddresses();
    foreach (var addr in addrs) {
      (addr.BindPort, addr.PublicIPv4) = await ConnectFromIPv4(addr.PrivateIPv4, port);
    }
    return addrs;
  }

  private async Task<Tuple<int, IPEndPoint?>> ConnectFromIPv4(IPAddress localIp, int localPort = 0, int tries = 0) {
    using var udpClient = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
    try {
      udpClient.Bind(new IPEndPoint(localIp, localPort));
      udpClient.ReceiveTimeout = Timeout;
    } catch (SocketException e) {
      if (localPort > 0 && tries < 10) {
        Log.Information("[STUN] failed to bind {ip}:{port}; trying next port...", localIp, localPort);
        return await ConnectFromIPv4(localIp, localPort + 1, tries + 1);
      }
      Log.Warning(e, "[STUN] failed to bind {ip}:{port}", localIp, localPort);
      return new Tuple<int, IPEndPoint?>(localPort, null);
    } catch (Exception e) {
      Log.Warning(e, "[STUN] failed to bind {ip}:{port}", localIp, localPort);
      return new Tuple<int, IPEndPoint?>(localPort, null);
    }

    List<Task<IPEndPoint?>> tasks = new List<Task<IPEndPoint?>>();
    foreach (var stunServer in _stunServers) {
      tasks.Add(ConnectToStun(localIp, udpClient, stunServer));
    }
    await Task.WhenAll(tasks);
    var endpoints = tasks.Select(t => t.Result).Where(ep => ep != null).ToList();
    if (endpoints.Count < 2) {
      if (endpoints.Any()) Log.Warning("[STUN] only got {cnt} STUN responses", endpoints.Count);
      if (!endpoints.Any()) return new Tuple<int, IPEndPoint?>(localPort, null);
    }
    if (endpoints.Count > 1 && endpoints[0]!.Port != endpoints[1]!.Port) {
      Log.Warning("[STUN] got different responses: {addr1} v. {addr2}; NAT punching may fail", endpoints[0], endpoints[1]);
    }
    return new Tuple<int, IPEndPoint?>(localPort, endpoints[0]);
  }

  private async Task<IPEndPoint?> ConnectToStun(IPAddress localIp, Socket udpClient, string stunServer) {
    try {
      // udpClient.Connect(remoteEp);
      var hostAddrs = await Dns.GetHostAddressesAsync(stunServer);
      var hostAddr = hostAddrs.FirstOrDefault(h => h.AddressFamily == AddressFamily.InterNetwork);
      if (hostAddr == null) {
        Log.Warning("[STUN] could not find inter-network address for {server} among {addrs}", stunServer, string.Join(", ", hostAddrs.Select(h => h.ToString())));
        hostAddr = hostAddrs.FirstOrDefault() ?? throw new ArgumentException($"No host address found for {stunServer}");
      }
      var remoteEp = new IPEndPoint(hostAddr, (int) StunPort);

      byte[] request = BuildBindingRequest();
      await udpClient.SendToAsync(new ArraySegment<byte>(request), SocketFlags.None, remoteEp);

      var buffer = new byte[512];
      var receiveTask = udpClient.ReceiveFromAsync(new ArraySegment<byte>(buffer), SocketFlags.None, remoteEp);
      var timeoutTask = Task.Delay(Timeout);
      var completed = await Task.WhenAny(receiveTask, timeoutTask);

      if (completed == receiveTask) {
        var received = receiveTask.Result;
        var trimmed = new byte[received.ReceivedBytes];
        Array.Copy(buffer, trimmed, trimmed.Length);

        if (TryParseBindingResponse(trimmed, out var publicEp)) {
          // Log.Information("[STUN] parsed {localIp} => {publicEp}", localIp, publicEp);
          return publicEp;
        } else {
          Log.Warning("[STUN] failed to parse response for {localIp}", localIp);
          return null;
        }
      } else {
        Log.Warning("[STUN] timed out waiting for {localIP}", localIp);
        return null;
      }
    } catch (Exception e) {
      Log.Warning(e, "[STUN] failed to load {localIp}", localIp);
      return null;
    }
  }

  static byte[] BuildBindingRequest() {
    byte[] buffer = new byte[20];
    buffer[0] = 0x00; // Binding request type
    buffer[1] = 0x01;
    buffer[2] = 0x00; // Message length (no attributes)
    buffer[3] = 0x00;
    buffer[4] = 0x21; // Magic cookie
    buffer[5] = 0x12;
    buffer[6] = 0xA4;
    buffer[7] = 0x42;

    // Transaction ID (12 bytes random)
    var rand = new Random();
    rand.NextBytes(buffer[8..20]);
    return buffer;
  }

  static bool TryParseBindingResponse(byte[] response, [NotNullWhen(true)] out IPEndPoint? result) {
    result = null;

    if (response.Length < 20 || response[0] != 0x01 || response[1] != 0x01)
      return false; // Not a binding success response

    int msgLength = (response[2] << 8) | response[3];
    int index = 20;

    while (index + 4 <= response.Length) {
      ushort attrType = (ushort)((response[index] << 8) | response[index + 1]);
      ushort attrLength = (ushort)((response[index + 2] << 8) | response[index + 3]);
      index += 4;

      if (attrType == 0x0020 || attrType == 0x0001) {// XOR-MAPPED-ADDRESS or MAPPED-ADDRESS
        byte family = response[index + 1];
        int port = (response[index + 2] << 8) | response[index + 3];
        byte[] ipBytes = new byte[4];
        Array.Copy(response, index + 4, ipBytes, 0, 4);

        if (attrType == 0x0020) {// XOR-MAPPED-ADDRESS
          port ^= 0x2112;
          for (int i = 0; i < 4; i++)
            ipBytes[i] ^= new byte[] { 0x21, 0x12, 0xA4, 0x42 }[i];
        }

        result = new IPEndPoint(new IPAddress(ipBytes), port);
        return true;
      }

      index += attrLength;
    }

    return false;
  }
}

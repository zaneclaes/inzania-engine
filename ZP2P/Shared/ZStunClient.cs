using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading.Tasks;
using IZ.Core.Contexts;

namespace IZ.P2P.Shared;

public class ZStunClient : LogicBase {
  public string StunServer { get; private set; }

  public uint StunPort { get; private set; }

  public ZStunClient(IZContext ctx, string stunServer = "stun.l.google.com", uint stunPort = 19302) : base(ctx) {
    StunServer = stunServer;
    StunPort = stunPort;
  }

  public List<IPAddress> GetLocalIpAddresses() {
    List<IPAddress> localIPs = new List<IPAddress>();

    foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces()) {
      if (ni.OperationalStatus != OperationalStatus.Up)
        continue;

      foreach (UnicastIPAddressInformation ipInfo in ni.GetIPProperties().UnicastAddresses) {
        if (ipInfo.Address.AddressFamily == AddressFamily.InterNetwork && !IsCgnat(ipInfo.Address) && ipInfo.Address.ToString() != "127.0.0.1") {
          localIPs.Add(ipInfo.Address);
        }
      }
    }
    return localIPs;
  }

  private static bool IsCgnat(IPAddress ip) =>
    ip.AddressFamily == AddressFamily.InterNetwork &&
    ip.GetAddressBytes()[0] == 100 &&
    (ip.GetAddressBytes()[1] >= 64 && ip.GetAddressBytes()[1] <= 127);

  public async Task<List<IPEndPoint>> GetConnectionOptions() {
    var hostAddr = await Dns.GetHostAddressesAsync(StunServer);
    var remoteEp = new IPEndPoint(hostAddr[0], (int) StunPort);
    List<IPAddress> localIPs = GetLocalIpAddresses();

    List<IPEndPoint> discoveredEndpoints = new List<IPEndPoint>();
    foreach (var localIp in localIPs) {
      try {
        using var udpClient = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        udpClient.Bind(new IPEndPoint(localIp, 0));
        udpClient.ReceiveTimeout = 2000;

        // udpClient.Connect(remoteEp);
        byte[] request = BuildBindingRequest();
        await udpClient.SendToAsync(new ArraySegment<byte>(request), SocketFlags.None, remoteEp);

        var buffer = new byte[512];
        var receiveTask = udpClient.ReceiveFromAsync(new ArraySegment<byte>(buffer), SocketFlags.None, remoteEp);
        var timeoutTask = Task.Delay(2000);
        var completed = await Task.WhenAny(receiveTask, timeoutTask);

        if (completed == receiveTask) {
          var received = receiveTask.Result;
          var trimmed = new byte[received.ReceivedBytes];
          Array.Copy(buffer, trimmed, trimmed.Length);

          if (TryParseBindingResponse(trimmed, out var publicEp)) {
            // Log.Information("[STUN] parsed {localIp} => {publicEp}", localIp, publicEp);
            discoveredEndpoints.Add(publicEp);
          } else {
            Log.Warning("[STUN] failed to parse response for {localIp}", localIp);
          }
        } else {
          Log.Warning("[STUN] timed out waiting for {localIP}", localIp);
        }

      } catch (SocketException e) {
        Log.Warning(e, "[STUN] failed to load {localIp}", localIp);
      }
    }
    return discoveredEndpoints;
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

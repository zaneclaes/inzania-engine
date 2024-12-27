#region

using System.Net.WebSockets;
using System.Threading.Tasks;

#endregion

namespace IZ.Client.Networking.Sockets;

public enum SocketCloseCode {
  /* Do NOT use NotSet - it's only purpose is to indicate that the close code cannot be parsed. */
  NotSet = 0,
  Normal = 1000,
  Away = 1001,
  ProtocolError = 1002,
  UnsupportedData = 1003,
  Undefined = 1004,
  NoStatus = 1005,
  Abnormal = 1006,
  InvalidData = 1007,
  PolicyViolation = 1008,
  TooBig = 1009,
  MandatoryExtension = 1010,
  ServerError = 1011,
  TlsHandshakeFailure = 1015
}

public delegate void SocketOpenEventHandler();
public delegate void SocketMessageEventHandler(byte[] data);
public delegate void SocketErrorEventHandler(string errorMsg);
public delegate void SocketCloseEventHandler(SocketCloseCode closeCode);

public interface ISocket {

  WebSocketState State { get; }
  event SocketOpenEventHandler OnOpen;
  event SocketMessageEventHandler OnMessage;
  event SocketErrorEventHandler OnError;
  event SocketCloseEventHandler OnClose;

  public void DispatchMessageQueue();

  public Task Connect();

  public Task Send(byte[] data);

  public Task Close();
}

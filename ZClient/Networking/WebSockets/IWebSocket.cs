#region

using System.Net.WebSockets;
using System.Threading.Tasks;

#endregion

namespace IZ.Client.Networking.WebSockets;

public delegate void SocketOpenEventHandler();
public delegate void SocketMessageEventHandler(byte[] data);
public delegate void SocketErrorEventHandler(string errorMsg);
public delegate void SocketCloseEventHandler(WebSocketCloseCode closeCode);

public interface IWebSocket {

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

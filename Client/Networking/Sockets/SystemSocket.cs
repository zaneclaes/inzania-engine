#region

using System;
using System.Net.WebSockets;
using System.Threading.Tasks;

#endregion

namespace IZ.Client.Networking.Sockets;

public class SystemSocket : ISocket {
#pragma warning disable 67
  public event SocketOpenEventHandler? OnOpen;
  public event SocketMessageEventHandler? OnMessage;
  public event SocketErrorEventHandler? OnError;
  public event SocketCloseEventHandler? OnClose;
#pragma warning restore 67

  public WebSocketState State => WebSocketState.None;

  public void DispatchMessageQueue() {
    throw new NotImplementedException();
  }

  public Task Connect() => throw new NotImplementedException();

  public Task Send(byte[] data) => throw new NotImplementedException();

  public Task Close() => throw new NotImplementedException();
}

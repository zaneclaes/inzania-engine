namespace IZ.Client.Networking.WebSockets;

public enum SocketState {
  Error = -2,
  Disconnected = -1,
  Ready = 0,
  Connecting = 1,
  Connected = 2,
  Subscribed = 3,
  Completed = 4
}

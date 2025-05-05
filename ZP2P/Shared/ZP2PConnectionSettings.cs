using System;

namespace IZ.P2P.Shared;

public class ZP2PConnectionSettings {
  // Fast pings for clock sync
  public TimeSpan PingInterval = TimeSpan.FromSeconds(0.25);

  public TimeSpan ConnectionTimeout = TimeSpan.FromSeconds(10);

  public TimeSpan ReconnectDelay = TimeSpan.FromSeconds(0.25);

  public int MaxConnectAttempts => (int) (ConnectionTimeout.TotalMilliseconds / ReconnectDelay.TotalMilliseconds);
}

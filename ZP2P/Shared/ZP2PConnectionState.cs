namespace IZ.P2P.Shared;

public enum ZP2PConnectionState {
  Closed,
  OpeningConnection,
  LoadingSession,
  WaitingForPeer,
  Connected,
}

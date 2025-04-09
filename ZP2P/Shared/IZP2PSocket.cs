namespace IZ.P2P.Shared;

public interface IZP2PSocket {
  public string? ContentType { get; }

  public ushort? PortNumber { get; }

  public bool IsRunning { get; }
}

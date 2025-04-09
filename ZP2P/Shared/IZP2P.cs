using IZ.P2P.Data;

namespace IZ.P2P.Shared;

public interface IZP2P {
  // public ushort? PortNumber { get; }

  public bool IsRunning { get; }

  // Who are we?
  public IZP2PMember Member { get; }
}

using IZ.P2P.Data;

namespace IZ.P2P.Shared;

public interface IZP2PConnection {
  // To whom are we connected?
  public IZP2PMember Member { get; }
}

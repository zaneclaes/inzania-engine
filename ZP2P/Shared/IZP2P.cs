using System.Collections.Generic;
using IZ.P2P.Data;

namespace IZ.P2P.Shared;

public interface IZP2P<TSession> where TSession : IZP2PSession {
  // public ushort? PortNumber { get; }

  public bool IsRunning { get; }

  // Who are we?
  public IZP2PMember Member { get; }

  public List<IZP2PMember> Members { get; }

  public TSession? Session { get; }
}

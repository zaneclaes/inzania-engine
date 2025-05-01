namespace IZ.P2P.Shared;

public interface IZP2PMessage<TSession, TMember> {
  public TSession Session { get; }

  public TMember? Member { get; }

  public bool IsSessionClosed { get; }

  public bool IsNewMember { get; }
}

namespace IZ.Core.Auth;

public interface IOwned { }

public interface IMightBeOwned : IOwned {
  public string? UserId { get; }
}

public interface IAmOwned : IOwned {
  public string UserId { get; }
}

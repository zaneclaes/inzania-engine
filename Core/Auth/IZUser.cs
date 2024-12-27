using IZ.Core.Data;

namespace IZ.Core.Auth;

public interface ITuneUser : IStringKeyData, ICreatedAt, IAmOwned {
  public TuneUserRole Role { get; }

  public string Username { get; }

  public string UsernameLower { get; }

  public string Email { get; }
}

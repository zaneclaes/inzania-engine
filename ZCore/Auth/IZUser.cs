using IZ.Core.Data;

namespace IZ.Core.Auth;

public interface IZUser : IStringKeyData, ICreatedAt, IAmOwned {
  public ZUserRole Role { get; }

  public string Username { get; }

  public string UsernameLower { get; }

  public string Email { get; }
}

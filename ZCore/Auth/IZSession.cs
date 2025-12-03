using System;
using IZ.Core.Contexts;
using IZ.Core.Data;

namespace IZ.Core.Auth;

public interface IZSession : IStringKeyData, ICreatedAt, IHaveContext {
  public IZUser IZUser { get; }

  public string Token { get; }

  public string InstallId { get; }

  public DateTime ExpiresAt { get; }

  public DateTime? DeletedAt { get; }

  public StoredSession ToStoredSession() => new StoredSession() {
    Context = Context,
    AccessToken = Token,
    UserId = IZUser.Id,
    Username = IZUser.Username,
    UserRole = IZUser.Role
  };
}

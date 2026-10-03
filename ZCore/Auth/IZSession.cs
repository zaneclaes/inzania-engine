using System;
using IZ.Core.Contexts;
using IZ.Core.Data;

namespace IZ.Core.Auth;

public interface ISoftDelete {
  public DateTime? DeletedAt { get; set; }
}

public interface IRefreshingSession {
  public string? RefreshToken { get; }
  public DateTime? AccessTokenExpiresAt { get; }
}

public interface IZSession : IStringKeyData, ICreatedAt, IHaveContext, ISoftDelete {
  public IZUser IZUser { get; }

  public string Token { get; }

  public string? InstallId { get; }

  public DateTime ExpiresAt { get; }

  public StoredSession ToStoredSession() => new StoredSession() {
    Context = Context,
    AccessToken = Token,
    RefreshToken = (this as IRefreshingSession)?.RefreshToken,
    AccessTokenExpiresAt = (this as IRefreshingSession)?.AccessTokenExpiresAt,
    UserId = IZUser.Id,
    Username = IZUser.Username,
    UserRole = IZUser.Role
  };
}

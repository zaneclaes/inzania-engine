using System;
using IZ.Core.Data;

namespace IZ.Core.Auth;

public interface IZSession : IStringKeyData, ICreatedAt {
  public IZUser IZUser { get; }

  public string Token { get; }

  public DateTime ExpiresAt { get; }
}

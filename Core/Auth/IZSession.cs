using System;
using IZ.Core.Data;

namespace IZ.Core.Auth;

public interface ITuneSession : IStringKeyData, ICreatedAt {
  public ITuneUser IZUser { get; }

  public string Token { get; }

  public DateTime ExpiresAt { get; }
}

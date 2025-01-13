using System.Collections.Generic;

namespace IZ.Core.Auth;

public class ZClientOptions {
  public string Id { get; set; } = null!;

  public string Secret { get; set; } = null!;

  public List<string> Scopes { get; set; } = new List<string>();
}

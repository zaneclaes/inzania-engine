using System.Collections.Generic;

namespace IZ.Core.Auth;

public class TuneClientOptions {
  public string Id { get; set; } = default!;

  public string Secret { get; set; } = default!;

  public List<string> Scopes { get; set; } = new List<string>();
}

using System.Collections.Generic;
using IZ.Core.Data.Attributes;

namespace IZ.Core.Auth;

public class ZAuthOptions {
  public const string Auth = "Auth";

  public string PublicUrl { get; set; } = default!;

  public string PrivateUrl { get; set; } = default!;

  [ApiIgnore]
  public string AdminSecret { get; set; } = default!;

  [ApiIgnore]
  public ZClientOptions ApiClient { get; set; } = default!;

  [ApiIgnore]
  public ZClientOptions WebClient { get; set; } = default!;

  public virtual List<ZClientOptions> AllClients => new List<ZClientOptions> {
    ApiClient,
    WebClient
  };
}

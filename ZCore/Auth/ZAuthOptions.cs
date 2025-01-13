using System.Collections.Generic;
using IZ.Core.Data.Attributes;

namespace IZ.Core.Auth;

public class ZAuthOptions {
  public const string Auth = "Auth";

  public string PublicUrl { get; set; } = null!;

  public string PrivateUrl { get; set; } = null!;

  [ApiIgnore]
  public string AdminSecret { get; set; } = null!;

  [ApiIgnore]
  public ZClientOptions ApiClient { get; set; } = null!;

  [ApiIgnore]
  public ZClientOptions WebClient { get; set; } = null!;

  public virtual List<ZClientOptions> AllClients => new List<ZClientOptions> {
    ApiClient,
    WebClient
  };
}

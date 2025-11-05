using System.Collections.Generic;
using IZ.Core.Data.Attributes;

namespace IZ.Core.Auth;

public class ZAuthOptions {
  public const string Auth = "Auth";

  [Observable] public string PublicUrl { get; set; } = null!;

  [Observable] public string PrivateUrl { get; set; } = null!;

  [OutputIgnore]
  public string AdminSecret { get; set; } = null!;

  [OutputIgnore]
  public ZClientOptions ApiClient { get; set; } = null!;

  [OutputIgnore]
  public ZClientOptions WebClient { get; set; } = null!;

  public virtual List<ZClientOptions> AllClients => new List<ZClientOptions> {
    ApiClient,
    WebClient
  };
}

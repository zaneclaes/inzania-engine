using System.Collections.Generic;
using IZ.Core.Data.Attributes;

namespace IZ.Core.Auth;

public class TuneAuthOptions {
  public const string Auth = "Auth";

  public string PublicUrl { get; set; } = default!;

  public string PrivateUrl { get; set; } = default!;

  [ApiIgnore]
  public string AdminSecret { get; set; } = default!;

  [ApiIgnore]
  public TuneClientOptions ApiClient { get; set; } = default!;

  [ApiIgnore]
  public TuneClientOptions WebClient { get; set; } = default!;

  public virtual List<TuneClientOptions> AllClients => new List<TuneClientOptions> {
    ApiClient,
    WebClient
  };
}

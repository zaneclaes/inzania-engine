using IZ.Core;
using IZ.Core.Auth;
using IZ.Core.Contexts;
using IZ.Core.Observability.Logging;

namespace IZ.Client;

public abstract class ZClientApp : ZApp {
  public string? InstallId { get; set; }

  protected ZClientApp(
    string productName, string domainName,
    ZEnvironment env, IZLogger? log = null, ZTarget? target = null,
    ApplicationStorage? directories = null, ZAuthOptions? authOptions = null
  ) : base(productName, domainName, env, log, target, directories, authOptions) { }
}

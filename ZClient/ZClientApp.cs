using IZ.Core;
using IZ.Core.Auth;
using IZ.Core.Contexts;
using IZ.Core.Observability.Logging;
using Semver;
using Tuneality.Core.Clients;

namespace IZ.Client;

public abstract class ZClientApp : ZApp {
  public string? ClientId { get; set; }

  public SemVersion? Version { get; set; }

  public TuneClientAppSettings Settings { get; protected set; } = new TuneClientAppSettings();

  protected ZClientApp(
    string productName, string domainName,
    ZEnvironment env, IZLogger? log = null, ZTarget? target = null,
    ApplicationStorage? directories = null, ZAuthOptions? authOptions = null
  ) : base(productName, domainName, env, log, target, directories, authOptions) { }
}

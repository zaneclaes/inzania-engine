using System;
using IZ.Core;
using IZ.Core.Contexts;
using IZ.Core.Json;
using IZ.Core.Observability.Logging;
using Semver;
using Tuneality.Core.Clients;

namespace IZ.Client;

public abstract class ZClientApp : ZApp {

  protected ZClientApp(
    string productName, string domainName,
    Func<IZContext, TuneClientAppSettings> settings, Func<IServiceProvider> fallbackServiceProviderFactory,
    ZEnvironment env, Func<ZLogBuilder>? logFactory = null, ZTarget? target = null
  ) : base(productName, domainName, settings, fallbackServiceProviderFactory, env, logFactory, target) {
    Settings = settings.Invoke(ZJson.DefaultContext);
  }
  public string? ClientId { get; set; }

  public SemVersion? Version { get; set; }

  public TuneClientAppSettings Settings { get; protected set; }
}

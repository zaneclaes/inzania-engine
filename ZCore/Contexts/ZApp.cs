#region

using System;
using System.Reflection;
using IZ.Core.Api.Fragments;
using IZ.Core.Auth;
using IZ.Core.Navigation;
using IZ.Core.Observability;
using IZ.Core.Observability.Logging;

#endregion

namespace IZ.Core.Contexts;

public abstract class ZApp : IGetLogged {
  public string ProductName { get; }

  public abstract IServiceProvider CreateServices();

  protected ZApp(
    string productName, string domainName,
    ZEnvironment env, IZLogger? log = null, ZTarget? target = null,
    ApplicationStorage? directories = null, ZAuthOptions? authOptions = null
  ) {
    ProductName = productName;
    Env = env;
    CoreAssembly = Assembly.GetExecutingAssembly();
    AppAssembly = Assembly.GetEntryAssembly() ?? CoreAssembly;
    Log = log ?? ZEnv.Log;
    Target = target ?? ZTarget.PublicApp;
    Storage = directories ?? new ApplicationStorage();
    Auth = authOptions ?? new ZAuthOptions();
    if (env <= ZEnvironment.Development) {
      DomainName = "localhost";
      SecureProtocol = false;
    } else {
      DomainName = domainName;
      SubDomain = env == ZEnvironment.Production ? "www" : env.ToString().ToLower();
      SecureProtocol = true;
    }
    ZEnv.App = this;
    // Sitemap = new Sitemap($"https://www.{ZEnv.DomainName}");
    ZEnv.SetRootContextSpawner(() => CreateServices().GetRootContext()); // new HostContext(this, builder.Services.BuildServiceProvider(), null)
  }

  public ZTarget Target { get; }

  public string TargetName => Target.ToString();

  public Assembly CoreAssembly { get; }

  public Assembly AppAssembly { get; }

  public string DomainName { get; }

  public string? SubDomain { get; }

  public bool SecureProtocol { get; }

  public IFragmentProvider Fragments {
    get => _fragmentProvider ??= new FragmentProvider(this);
    set => _fragmentProvider = value;
  }
  private IFragmentProvider? _fragmentProvider;

  public string Fqdn => $"{(SubDomain == null ? "" : $"{SubDomain}.")}{DomainName}{(DomainName == "localhost" ? ":5292" : "")}";

  public string Url => $"{(SecureProtocol ? "https" : "http")}://{Fqdn}";

  public string Cdn => Env <= ZEnvironment.Development ? Url :
    $"https://{(Env == ZEnvironment.Production ? "assets" : "assets-staging")}.{ZEnv.DomainName}";

  public string Gql => $"{Url}/api/graphql";

  public ZAuthOptions Auth { get; }

  // public ZEnvironment Env {
  //   get {
  //     if (Target <= TuneTarget.UnitTests) return ZEnvironment.Testing;
  //     if (Target <= TuneTarget.InternalApp) return ZEnvironment.Internal;
  //     if (Target <= TuneTarget.Server) return ZEnv.Environment ?? ZEnvironment.Production;
  //     // public app...
  //     return ZEnv.GetEnvironment("TUNEALITY_ENVIRONMENT") ?? ZEnvironment.Production;
  //   }
  // }


  public ZEnvironment Env { get; }

  public string EnvName => Env.ToString();

  public ApplicationStorage Storage { get; }


  public IZLogger Log { get; private set; }
  //
  // public TunealityApp ReplaceLogger(IZLogger log) {
  //   Log = log;
  //   return this;
  // }
}

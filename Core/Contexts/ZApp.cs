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
    IZEnvironment env, ITuneLogger? log = null, TuneTarget? target = null,
    ApplicationStorage? directories = null, TuneAuthOptions? authOptions = null
  ) {
    ProductName = productName;
    Env = env;
    CoreAssembly = Assembly.GetExecutingAssembly();
    AppAssembly = Assembly.GetEntryAssembly() ?? CoreAssembly;
    Log = log ?? IZEnv.Log;
    Target = target ?? TuneTarget.PublicApp;
    Storage = directories ?? new ApplicationStorage();
    Auth = authOptions ?? new TuneAuthOptions();
    if (env <= IZEnvironment.Development) {
      DomainName = "localhost";
      SecureProtocol = false;
    } else {
      DomainName = domainName;
      SubDomain = env == IZEnvironment.Production ? "www" : env.ToString().ToLower();
      SecureProtocol = true;
    }
    IZEnv.App = this;
    // Sitemap = new Sitemap($"https://www.{IZEnv.DomainName}");
    IZEnv.SetRootContextSpawner(() => CreateServices().GetRootContext()); // new HostContext(this, builder.Services.BuildServiceProvider(), null)
  }

  public TuneTarget Target { get; }

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

  public string Cdn => Env <= IZEnvironment.Development ? Url :
    $"https://{(Env == IZEnvironment.Production ? "assets" : "assets-staging")}.{IZEnv.DomainName}";

  public string Gql => $"{Url}/api/graphql";

  public TuneAuthOptions Auth { get; }

  // public IZEnvironment Env {
  //   get {
  //     if (Target <= TuneTarget.UnitTests) return IZEnvironment.Testing;
  //     if (Target <= TuneTarget.InternalApp) return IZEnvironment.Internal;
  //     if (Target <= TuneTarget.Server) return IZEnv.Environment ?? IZEnvironment.Production;
  //     // public app...
  //     return IZEnv.GetEnvironment("TUNEALITY_ENVIRONMENT") ?? IZEnvironment.Production;
  //   }
  // }


  public IZEnvironment Env { get; }

  public string EnvName => Env.ToString();

  public ApplicationStorage Storage { get; }


  public ITuneLogger Log { get; private set; }
  //
  // public TunealityApp ReplaceLogger(ITuneLogger log) {
  //   Log = log;
  //   return this;
  // }
}

#region

using System;
using System.Reflection;
using IZ.Core.Api;
using IZ.Core.Api.Fragments;
using IZ.Core.Auth;
using IZ.Core.Exceptions;
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
    Storage = directories ?? new ApplicationStorage(ProductName);
    Auth = authOptions ?? new ZAuthOptions();
    if (env <= ZEnvironment.Development) {
      DomainName = "localhost";
      SecureProtocol = false;
    } else {
      DomainName = domainName;
      SubDomain = env == ZEnvironment.Production ? "www" : env.ToString().ToLower();
      SecureProtocol = true;
    }
    ProductDomainName = domainName;
    ZEnv.App = this;
    // Sitemap = new Sitemap($"https://www.{ZEnv.DomainName}");
    ZEnv.SetRootContextSpawner(() => CreateServices().GetRootContext()); // new HostContext(this, builder.Services.BuildServiceProvider(), null)
    ZApi.EnsureSchema();
  }

  public virtual bool HandleZException(ZException e) { return false; }

  public ZTarget Target { get; }

  public string TargetName => Target.ToString();

  public Assembly CoreAssembly { get; }

  public Assembly AppAssembly { get; }

  public string DomainName { get; }

  public string ProductDomainName { get; }

  public string? SubDomain { get; }

  public bool SecureProtocol { get; }

  public IFragmentProvider Fragments {
    get => _fragmentProvider ??= new FragmentProvider(this);
    set => _fragmentProvider = value;
  }
  private IFragmentProvider? _fragmentProvider;

  public string Fqdn => $"{(SubDomain == null ? "" : $"{SubDomain}.")}{DomainName}{(DomainName == "localhost" ? ":5292" : "")}";

  private string HttpProtocol => SecureProtocol ? "https" : "http";

  public string Url => $"{HttpProtocol}://{Fqdn}";

  public string Cdn => $"https://{(Env == ZEnvironment.Production ? "assets" : "assets-staging")}.{ProductDomainName}";

  private string GqlFqdn => Env == ZEnvironment.Production ? $"production.{DomainName}" : Fqdn;

  public string Gql => $"{HttpProtocol}://{GqlFqdn}/api/graphql";

  public ZAuthOptions Auth { get; }

  public ZEnvironment Env { get; }

  public string EnvName => Env.ToString();

  public ApplicationStorage Storage { get; }

  public IZLogger Log { get; private set; }
}

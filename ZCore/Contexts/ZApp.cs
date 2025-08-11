#region

using System;
using System.Diagnostics;
using System.Reflection;
using IZ.Core.Api;
using IZ.Core.Api.Fragments;
using IZ.Core.Auth;
using IZ.Core.Exceptions;
using IZ.Core.Observability;
using IZ.Core.Observability.Logging;
// ReSharper disable VirtualMemberCallInConstructor

#endregion

namespace IZ.Core.Contexts;

public interface IZAppSettings {
  public ApplicationStorage? Storage { get; }
  public ZAuthOptions? Auth { get; }
}

public abstract class ZApp : IGetLogged, IDisposable {

  private readonly IZAppSettings? _appSettings;

  private readonly Func<IServiceProvider> _fallbackServiceProviderFactory;
  private IFragmentProvider? _fragmentProvider;

  public readonly Stopwatch Uptimer = Stopwatch.StartNew();
  public TimeSpan Uptime => TimeSpan.FromMilliseconds(Uptimer.Elapsed.TotalMilliseconds);

  protected ZApp(
    string productName, string domainName,
    Func<IZContext, IZAppSettings> settingsBuilder,
    Func<IServiceProvider> fallbackServiceProviderFactory,
    ZEnvironment env, Func<ZLogBuilder>? logFactory = null, ZTarget? target = null
  ) {
    ProductName = productName;
    Env = env;
    _fallbackServiceProviderFactory = fallbackServiceProviderFactory;
    CoreAssembly = Assembly.GetExecutingAssembly();
    AppAssembly = Assembly.GetEntryAssembly() ?? CoreAssembly;
    Log = logFactory?.Invoke().BuildToSingleton() ?? ZEnv.Log;
    Target = target ?? ZTarget.PublicApp;
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

    // Actually build the app settings, including storage and auth
    var ctxt = new WorkContext(this);
    _appSettings = settingsBuilder.Invoke(ctxt);
    Storage = _appSettings?.Storage ?? new ApplicationStorage(ProductName);
    Auth = _appSettings?.Auth ?? new ZAuthOptions();

    ZApi.EnsureSchema();
  }
  public string ProductName { get; }

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

  public virtual IServiceProvider CreateServices() => _fallbackServiceProviderFactory.Invoke();

  public virtual bool HandleZException(ZException e) => false;

  public virtual void Dispose() {
    Storage.Dispose();
  }
}

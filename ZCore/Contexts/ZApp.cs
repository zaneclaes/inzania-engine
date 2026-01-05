#region

using System;
using System.Diagnostics;
using System.Reflection;
using System.Threading.Tasks;
using IZ.Core.Api;
using IZ.Core.Api.Fragments;
using IZ.Core.Auth;
using IZ.Core.Exceptions;
using IZ.Core.Observability;
using IZ.Core.Observability.Analytics;
using IZ.Core.Observability.Logging;
using IZ.Core.Utils;
// ReSharper disable VirtualMemberCallInConstructor

#endregion

namespace IZ.Core.Contexts;

public interface IZAppSettings {
  public ApplicationStorage? Storage { get; }
  public ZAuthOptions? Auth { get; }
  public AnalyticsOptions? GoogleAnalytics { get; }
}

public class ZProfiled : IDisposable {
  public void Dispose() { }
}

public abstract class ZApp : IGetLogged, IDisposable {

  public IZAppSettings Settings => _appSettings ??
                                   throw new NullReferenceException(nameof(Settings));
  private IZAppSettings? _appSettings;
  private readonly Func<IZContext, ZTask<IZAppSettings>> _settingsBuilder;

  private readonly Func<IServiceProvider> _fallbackServiceProviderFactory;
  private IFragmentProvider? _fragmentProvider;

  public virtual bool IsRootSingleton => false;

  public readonly Stopwatch Uptimer;
  public TimeSpan Uptime => TimeSpan.FromMilliseconds(Uptimer.Elapsed.TotalMilliseconds);

  protected ZApp(
    string productName, string domainName,
    Func<IZContext, ZTask<IZAppSettings>> settingsBuilder,
    Func<IServiceProvider> fallbackServiceProviderFactory,
    ZEnvironment env, Func<ZLogBuilder>? logFactory = null, ZTarget? target = null,
    IZTypeMap? typeMap = null, Stopwatch? uptimer = null
  ) {
    Uptimer = uptimer ?? Stopwatch.StartNew();
    if (typeMap != null) ZApi.TypeMap = typeMap;
    ProductName = productName;
    _settingsBuilder = settingsBuilder;
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
      SubDomain = env == ZEnvironment.Production ? null : env.ToString().ToLower();
      SecureProtocol = true;
    }
    ProductDomainName = domainName;
    ZEnv.App = this;
    ZEnv.SetRootContextSpawner(() => CreateServices().GetRootContext()); // new HostContext(this, builder.Services.BuildServiceProvider(), null)
  }

  protected virtual async ZTask BuildAsync() {
    // ZApi.EnsureSchemaAsync().Forget();
    var ctxt = new WorkContext(this);
    _appSettings = await _settingsBuilder.Invoke(ctxt);
    _storage = _appSettings?.Storage ?? new ApplicationStorage(ProductName);
    _auth = _appSettings?.Auth ?? new ZAuthOptions();
  }

  // Actually build the app settings, including storage and auth
  protected virtual ZTask PrepareAsync() {
    // await ZApi.WaitForSchema();
    return ZTask.CompletedTask;
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

  private string ApiFqdn => Env == ZEnvironment.Production ? $"production.{DomainName}" : Fqdn;

  public string ApiUrl => $"{HttpProtocol}://{ApiFqdn}";

  public string Gql => $"{ApiUrl}/api/graphql";

  public ZAuthOptions Auth => _auth ??
                              throw new NullReferenceException(nameof(Auth));
  private ZAuthOptions? _auth;

  public ZEnvironment Env { get; }

  public string EnvName => Env.ToString();

  public ApplicationStorage Storage => _storage ??
                                       throw new NullReferenceException(nameof(Storage));
  private ApplicationStorage? _storage;

  public IZLogger Log { get; private set; }

  public virtual IServiceProvider CreateServices() => _fallbackServiceProviderFactory.Invoke();

  public virtual bool HandleZException(ZException e) => false;

  public virtual IDisposable Profile(string func) => new ZProfiled();

  public virtual void Dispose() {
    Storage.Dispose();
  }
}

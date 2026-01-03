#region

using IZ.Core;
using IZ.Core.Api;
using IZ.Core.Auth;
using IZ.Core.Contexts;
using IZ.Core.Observability.Analytics;
using IZ.Core.Observability.Logging;
using IZ.Core.Utils;
using IZ.Logging.SerilogLogging;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Xunit.Abstractions;

#endregion

namespace ZTests;

public class ZTestAppSettings : IZAppSettings {
  public ApplicationStorage? Storage { get; set; }
  public ZAuthOptions? Auth { get; set; }
  public AnalyticsOptions? GoogleAnalytics { get; }
}

public class ZTestApp : ZApp {

  protected IServiceCollection _serviceCollection;

  public ZTestApp(
    string appName, string domainName, ZLogBuilder logBuilder, IServiceCollection services, ApplicationStorage? directories = null
  ) : base(appName + "Test", domainName, c => ZTask<IZAppSettings>.FromResult(new ZTestAppSettings {
    Storage = directories
  }), services.BuildServiceProvider, ZEnvironment.Testing, () => logBuilder, ZTarget.UnitTests) {
    _serviceCollection = services
      .AddSingleton<ZApp>(this);
  }

  protected virtual SerilogZLogBuilder GetLogBuilder() => SerilogZLogBuilder.GetDefault();

  public IZLogger GetLoggerForTestOutput(ITestOutputHelper output) =>
    GetLogBuilder().WriteTo(c => c.TestOutput(output)).Build();

  public async ZTask GetReadyAsync() {
    await BuildAsync();
    await PrepareAsync();
    await ZApi.EnsureSchemaAsync();
  }

  // public override IServiceProvider CreateServices() => _services.BuildServiceProvider();
  // private readonly IServiceCollection _services;
}

#region

using IZ.Core;
using IZ.Core.Auth;
using IZ.Core.Contexts;
using IZ.Core.Observability.Logging;
using IZ.Logging.SerilogLogging;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Xunit.Abstractions;

#endregion

namespace ZTests;

public class ZTestAppSettings : IZAppSettings {
  public ApplicationStorage? Storage { get; set; }
  public ZAuthOptions? Auth { get; set; }
}

public class ZTestApp : ZApp {

  public ZTestApp(
    string appName, string domainName, ZLogBuilder logBuilder, ServiceCollection services, ApplicationStorage? directories = null
  ) : base(appName + "Test", domainName, c => new ZTestAppSettings {
    Storage = directories
  }, services.BuildServiceProvider, ZEnvironment.Testing, () => logBuilder, ZTarget.UnitTests) { }

  protected virtual SerilogZLogBuilder GetLogBuilder() => SerilogZLogBuilder.GetDefault();

  public IZLogger GetLoggerForTestOutput(ITestOutputHelper output) =>
    GetLogBuilder().WriteTo(c => c.TestOutput(output)).Build();

  // public override IServiceProvider CreateServices() => _services.BuildServiceProvider();
  // private readonly IServiceCollection _services;
}

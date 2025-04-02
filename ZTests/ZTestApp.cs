using IZ.Core;
using IZ.Core.Contexts;
using IZ.Core.Observability.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace ZTests;

public class ZTestApp : ZApp {
  public ZTestApp(
    string appName, string domainName, IZLogger log, ApplicationStorage? directories = null
  ) : base(appName + "Test", domainName, ZEnvironment.Testing, log, ZTarget.UnitTests, directories) {
    _services = new ServiceCollection();
  }

  public override IServiceProvider CreateServices() => _services.BuildServiceProvider();
  private readonly IServiceCollection _services;
}

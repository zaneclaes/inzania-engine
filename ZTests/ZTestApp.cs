using IZ.Core;
using IZ.Core.Auth;
using IZ.Core.Contexts;
using IZ.Core.Observability.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace ZTests;

public class ZTestAppSettings : IZAppSettings {
  public ApplicationStorage? Storage { get; set; }
  public ZAuthOptions? Auth { get; set; }
}

public class ZTestApp : ZApp {
  public ZTestApp(
    string appName, string domainName, IZLogger log, ApplicationStorage? directories = null
  ) : base(appName + "Test", domainName, (c) => new ZTestAppSettings {
    Storage = directories,
  },() => throw new NotImplementedException(nameof(CreateServices)), ZEnvironment.Testing, log, ZTarget.UnitTests) {
    _services = new ServiceCollection();
  }

  public override IServiceProvider CreateServices() => _services.BuildServiceProvider();
  private readonly IServiceCollection _services;
}

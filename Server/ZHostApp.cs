#region

using System;
using System.Reflection;
using System.Threading.Tasks;
using IZ.Core;
using IZ.Core.Auth;
using IZ.Core.Contexts;
using IZ.Core.Observability.Logging;
using IZ.Logging.SerilogLogging;
using IZ.Observability.DataDog;
using IZ.Server.Requests;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Serilog.Settings.Configuration;
using Serilog.Sinks.Datadog.Logs;
using Serilog.Sinks.SystemConsole.Themes;
using Tuneality.Core;

#endregion

namespace IZ.Server;

public abstract class ZHostApp : ZApp {
  protected WebApplication? WebApp { get; private set; } = default!;

  private readonly WebApplicationBuilder _builder;

  protected ZHostApp(string productName, string domainName, WebApplicationBuilder builder) :
    base(productName, domainName, Enum.Parse<IZEnvironment>(Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")!), new SerilogLogBuilder()
        .WithTuneData()
        // .WithTuneXmlLogging()
        .ReadFrom(c => c.Configuration(builder.Configuration, new ConfigurationReaderOptions(
          Assembly.GetExecutingAssembly(), typeof(DatadogSink).Assembly, typeof(ConsoleTheme).Assembly)))
        .BuildToSingleton(),
      TuneTarget.PublicApp,
      builder.Configuration.GetSection("Dir").ToTunealityApplicationDirectories(),
      builder.Configuration.GetSection("Auth").Get<TuneAuthOptions>()) {
    DataDogTracing.Enable();

    _builder = builder;
    builder.Services.AddTuneServerCore(this);
  }

  public override IServiceProvider CreateServices() => WebApp?.Services ?? _builder.Services.BuildServiceProvider();

  protected virtual Task PrepareAsync() {
    WebApp!.UseSerilogRequestLogging(opts => {
      opts.GetLevel = ApiExceptionMiddleware.GetLogLevel;
    });
    return Task.CompletedTask;
  }

  public async Task RunAsync() {
    WebApp = _builder.Build();
    await PrepareAsync();
    await WebApp.RunAsync();
  }
}

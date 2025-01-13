#region

using System;
using System.Reflection;
using System.Threading.Tasks;
using IZ.Core;
using IZ.Core.Api;
using IZ.Core.Api.Fragments;
using IZ.Core.Auth;
using IZ.Core.Contexts;
using IZ.Core.Data.Seeds;
using IZ.Core.Observability.Logging;
using IZ.Core.Utils;
using IZ.Data.Providers;
using IZ.Logging.SerilogLogging;
using IZ.Observability.DataDog;
using IZ.Server.Requests;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Serilog.Settings.Configuration;
using Serilog.Sinks.Datadog.Logs;
using Serilog.Sinks.SystemConsole.Themes;

#endregion

namespace IZ.Server;

public abstract class ZHostApp<TDb> : ZApp where TDb : DbContext {
  protected WebApplication? WebApp { get; private set; } = null!;

  private readonly WebApplicationBuilder _builder;

  protected abstract DataSeed[] DataSeeds { get; }

  protected ZHostApp(string productName, string domainName, WebApplicationBuilder builder) :
    base(productName, domainName, Enum.Parse<ZEnvironment>(Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")!), new SerilogLogBuilder()
        .WithTuneData()
        // .WithTuneXmlLogging()
        .ReadFrom(c => c.Configuration(builder.Configuration, new ConfigurationReaderOptions(
          Assembly.GetExecutingAssembly(), typeof(DatadogSink).Assembly, typeof(ConsoleTheme).Assembly)))
        .BuildToSingleton(),
      ZTarget.PublicApp,
      builder.Configuration.GetSection("Dir").ToTunealityApplicationDirectories(),
      builder.Configuration.GetSection("Auth").Get<ZAuthOptions>()) {
    DataDogTracing.Enable();

    _builder = builder;
    builder.Services.AddTuneServerCore(this);
  }

  public override IServiceProvider CreateServices() => WebApp?.Services ?? _builder.Services.BuildServiceProvider();

  protected void AddWorker<T>(WebApplication app, TimeSpan? ts = null) where T : ContextualObject, IForeverTask, new() {
    var scopeFactory = app.Services.GetRequiredService<IServiceScopeFactory>();
    scopeFactory.ForeverLoop<T>(ts ?? TimeSpan.FromSeconds(15));
  }

  protected virtual void AddHealthChecks(WebApplication app) {
    app.MapHealthChecks("/health/readiness", new HealthCheckOptions {
      Predicate = check => check.Tags.Contains("readiness"),
      ResponseWriter = HealthCheck.WriteResponse
    });
    app.MapHealthChecks("/health/liveness", new HealthCheckOptions {
      Predicate = check => check.Tags.Contains("liveness"),
      ResponseWriter = HealthCheck.WriteResponse
    });
    app.MapHealthChecks("/health", new HealthCheckOptions {
      Predicate = check => check.Tags.Contains("liveness"),
      ResponseWriter = HealthCheck.WriteResponse
    });
  }

  protected virtual async Task PrepareAsync(WebApplication app) {
    app.UseSerilogRequestLogging(opts => {
      opts.GetLevel = ApiExceptionMiddleware.GetLogLevel;
    });
    app.Services.GetRequiredService<IFragmentProvider>().LoadDirectory(Storage.GraphQLDir);

    await app.Services.MigrateDatabaseAsync<TDb>();
    await app.Services.SeedDatabaseAsync(DataSeeds);
  }

  public async Task RunAsync() {
    WebApp = _builder.Build();
    await PrepareAsync(WebApp);
    await WebApp.RunAsync();
  }
}

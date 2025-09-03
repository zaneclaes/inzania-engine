#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using IZ.Core;
using IZ.Core.Api.Fragments;
using IZ.Core.Auth;
using IZ.Core.Contexts;
using IZ.Core.Data.Seeds;
using IZ.Core.Observability.Analytics;
using IZ.Core.Observability.Logging;
using IZ.Core.Utils;
using IZ.Data.Providers;
using IZ.Logging.SerilogLogging;
using IZ.Observability.DataDog;
using IZ.Server.Requests;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Serilog.Settings.Configuration;
using Serilog.Sinks.Datadog.Logs;
using Serilog.Sinks.SystemConsole.Themes;

#endregion

namespace IZ.Server;

public class HostAppSettings : IZAppSettings {

  public HostAppSettings(string productName, ConfigurationManager config) {
    Storage = config.GetSection("Dir").ToZApplicationDirectories(productName);
    Auth = config.GetSection("Auth").Get<ZAuthOptions>();
  }

  public ApplicationStorage? Storage { get; }
  public ZAuthOptions? Auth { get; }
  public AnalyticsOptions? GoogleAnalytics { get; }
}

public abstract class ZHostApp<TDb> : ZApp where TDb : DbContext {

  private readonly WebApplicationBuilder _builder;

  protected ZHostApp(string productName, string domainName, WebApplicationBuilder builder) : base(
    productName,
    domainName,
    c => new HostAppSettings(productName, builder.Configuration),
    () => builder.Services.BuildServiceProvider(),
    Enum.Parse<ZEnvironment>(Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")!),
    () => CreateLogger(builder.Configuration),
    ZTarget.PublicApp
  ) {
    DataDogTracing.Enable();

    _builder = builder;
    builder.Services.AddZServerCore(this);
  }
  protected WebApplication? WebApp { get; private set; }

  protected abstract DataSeed[] DataSeeds { get; }

  private static ZLogBuilder CreateLogger(IConfiguration config) => SerilogZLogBuilder.GetDefault()
    .ReadFrom(c => c.Configuration(config, new ConfigurationReaderOptions(
      Assembly.GetExecutingAssembly(), typeof(DatadogSink).Assembly, typeof(ConsoleTheme).Assembly)));

  public override IServiceProvider CreateServices() => WebApp?.Services ?? base.CreateServices();

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

    // Seeding should not block startup:
    app.Services.SeedDatabaseAsync(DataSeeds).Forget();

    app.Lifetime.ApplicationStarted.Register(() => ListUrls(app));
  }

  protected void ListUrls(WebApplication app) {
    ICollection<string> serverAddresses = app.Urls;
    if (!serverAddresses.Any()) {
      // If app.Urls is empty, try getting addresses from the server features
      var server = app.Services.GetRequiredService<IServer>();
      var addressesFeature = server.Features.Get<IServerAddressesFeature>();
      serverAddresses = addressesFeature?.Addresses ?? new List<string>();
    }
    Log.Information("[SERVER] hosting on: {urls}", serverAddresses);
  }

  public async Task RunAsync() {
    WebApp = _builder.Build();
    await PrepareAsync(WebApp);
    await WebApp.RunAsync();
  }
}

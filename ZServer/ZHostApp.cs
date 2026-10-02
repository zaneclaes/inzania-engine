#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using IZ.Core;
using IZ.Core.Api;
using IZ.Core.Api.Fragments;
using IZ.Core.Auth;
using IZ.Core.Contexts;
using IZ.Core.Data.Seeds;
using IZ.Core.Observability;
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
using Microsoft.Extensions.Hosting;
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

  protected readonly WebApplicationBuilder _builder;

  protected ZHostApp(string productName, string domainName, WebApplicationBuilder builder, IZTypeMap? typeMap = null) : base(
    productName,
    domainName,
    c => ZTask<IZAppSettings>.FromResult(new HostAppSettings(productName, builder.Configuration)),
    () => builder.Services.BuildServiceProvider(),
    Enum.Parse<ZEnvironment>(Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")!),
    () => CreateLogger(builder.Configuration),
    ZTarget.PublicApp, typeMap
  ) {
    DataDogTracing.Enable();
    _builder = builder;
  }

  protected override async ZTask BuildAsync() {
    await base.BuildAsync();
    _builder.Services.AddZServerCore(this);
  }

  /// <summary>
  /// Builds the application's DI container and NOTHING else — no migrations, no seeds, no workers,
  /// no HTTP listener, no database connection (all of which live in the host's own start-up path).
  ///
  /// <para>This exists for tooling that needs the app's REAL registrations without running the app:
  /// <see cref="Design.ZDesignTimeDbContextFactory{TApp,TDb}" /> (so `dotnet ef` can construct a
  /// context whose dependencies only this container can supply) and any snapshot/codegen tool a
  /// consuming project writes. The caller owns the returned provider and must dispose it.</para>
  /// </summary>
  public async Task<ServiceProvider> BuildServicesAsync() {
    await BuildAsync();
    return _builder.Services.BuildServiceProvider();
  }

  protected WebApplication? WebApp { get; private set; }

  private bool _isListening;

  /// <summary>
  /// Starts Kestrel after the application has mapped its pipeline, without pretending that all of
  /// the application's own initialization is complete. A host that has done this must wait for
  /// shutdown rather than ask <see cref="WebApplication.RunAsync"/> to start Kestrel a second time.
  /// </summary>
  protected async Task StartListeningAsync() {
    if (_isListening) return;
    await WebApp!.StartAsync();
    _isListening = true;
  }

  protected abstract IDataSeed[] DataSeeds { get; }

  private static ZLogBuilder CreateLogger(IConfiguration config) => SerilogZLogBuilder.GetDefault()
    .ReadFrom(c => c.Configuration(config, new ConfigurationReaderOptions(
      Assembly.GetExecutingAssembly(), typeof(DatadogSink).Assembly, typeof(ConsoleTheme).Assembly)));

  public override IServiceProvider CreateServices() => WebApp?.Services ?? base.CreateServices();

  protected void AddWorker<T>(WebApplication app, TimeSpan? ts = null) where T : ContextualObject, IForeverTask, new() {
    var scopeFactory = app.Services.GetRequiredService<IServiceScopeFactory>();
    scopeFactory.ForeverLoop<T>(ts ?? TimeSpan.FromSeconds(15));
  }

  protected virtual void AddHealthChecks(WebApplication app) {
    app.MapHealthChecks("/health/readiness", HealthCheck.Readiness());
    app.MapHealthChecks("/health/liveness", new HealthCheckOptions {
      Predicate = check => check.Tags.Contains("liveness"),
      ResponseWriter = HealthCheck.WriteResponse
    });
    app.MapHealthChecks("/health", new HealthCheckOptions {
      Predicate = check => check.Tags.Contains("liveness"),
      ResponseWriter = HealthCheck.WriteResponse
    });
  }

  /// <summary>
  /// The configuration faults this host starts with: every environment name that binds nothing
  /// (<see cref="ConfigCheck.UnboundEnvironmentNames" />). A subclass appends its own (a key pair
  /// whose modes disagree, a missing secret) and they share one rule, <see cref="ConfigCheck.Apply" />.
  /// Never include a value.
  /// </summary>
  protected virtual IEnumerable<string> ConfigProblems() =>
    ConfigCheck.UnboundEnvironmentNames(_builder.Configuration, Environment.GetEnvironmentVariables())
      .Select(ConfigCheck.Unbound);

  private bool _hostPrepared;

  /// <summary>Completes the parts of host preparation that must happen before Kestrel starts:
  /// middleware registration and the fragment catalogue. Database initialization intentionally
  /// remains separate so a derived host can expose a non-ready listener while it performs a
  /// database preparation.</summary>
  protected async ZTask PrepareBeforeListeningAsync() {
    if (_hostPrepared) return;
    await base.PrepareAsync();
    using var config = new WorkContext(this, "Config");
    ConfigCheck.Apply(ConfigProblems().ToList(), Env, Log, _ => config.IncrementMetric("config.problem",
      tags: new Dictionary<string, object> { ["app"] = ProductName }));
    WebApp!.UseSerilogRequestLogging(opts => {
      opts.GetLevel = ApiExceptionMiddleware.GetLogLevel;
    });
    WebApp!.Services.GetRequiredService<IFragmentProvider>().LoadDirectory(Storage.GraphQLDir);
    _hostPrepared = true;
  }

  /// <summary>Applies the database schema and starts non-blocking seeding after the host pipeline is fixed.</summary>
  protected async ZTask PrepareDatabaseAsync() {
    await WebApp!.Services.MigrateDatabaseAsync<TDb>();
    LaunchDatabaseSeeds(WebApp!.Services);
    WebApp!.Lifetime.ApplicationStarted.Register(() => ListUrls(WebApp!));
  }

  /// <summary>Starts the ordinary seed pass without holding host preparation. A host with a
  /// background refresh may defer this launch until that refresh reaches its terminal result.</summary>
  protected virtual void LaunchDatabaseSeeds(IServiceProvider services) => SeedDatabaseAsync(services).Forget();

  /// <summary>The existing seed runner, shared by immediate and deferred startup launches.</summary>
  protected virtual Task SeedDatabaseAsync(IServiceProvider services) => services.SeedDatabaseAsync(DataSeeds);

  protected override async ZTask PrepareAsync() {
    await PrepareBeforeListeningAsync();
    await PrepareDatabaseAsync();
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
    await BuildAsync();
    WebApp = _builder.Build();
    await PrepareAsync();
    if (_isListening) await WebApp.WaitForShutdownAsync();
    else await WebApp.RunAsync();
  }
}

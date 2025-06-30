using System.Reflection;
using IZ.Core;
using IZ.Core.Contexts;
using IZ.Core.Observability.Logging;
using IZ.Core.Utils;
using IZ.Logging.SerilogLogging;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Xunit.Abstractions;

namespace ZTests;

public abstract class ZTest<ZTA> : LogicBase where ZTA : ZTestApp {
  private ZTestRootContext _rootContext;

  protected ZTA App { get;  private set; }

  // Where the test project lives
  protected virtual string TestProjectDir => Path.Combine("..", "..", "..");

  // If part of solution, overwrite, preferring git path as root
  protected virtual string SolutionDir => TestProjectDir;

  protected virtual string UserDir => Path.Combine(MonoRepoRoot, "User");

  protected string MonoRepoRoot => SolutionDir;

  protected ZTest(ZTA app) {
    App = app;
    Context = _rootContext = new ZTestRootContext(App, new ServiceCollection()
      .AddZApp<ZTA, RootContext>(App)
      .BuildServiceProvider());

    ZEnv.SetRootContextSpawner(() => _rootContext);
    ZEnv.App = app;
  }

  ~ZTest() {
    // Root.Log.Information("[TEST] shutting down...");
    // TuneConfig.DatadogLogSink!.DisposeAsync().ConfigureAwait(true);
    Task.Delay(15000).Wait();
    // Root.Log.Information("[TEST] done");
  }


  protected override string ContextualObjectGroup => "Test";
}

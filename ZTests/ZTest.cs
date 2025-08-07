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

public abstract class ZTest<TA> : LogicBase where TA : ZTestApp {

  protected TA App { get;  private set; }

  // Where the test project lives
  protected virtual string TestProjectDir => Path.Combine("..", "..", "..");

  // If part of solution, overwrite, preferring git path as root
  protected virtual string SolutionDir => TestProjectDir;

  protected virtual string UserDir => Path.Combine(MonoRepoRoot, "User");

  protected string MonoRepoRoot => SolutionDir;

  protected ZTest(TA app, ITestOutputHelper output) {
    ZTestRootContext rootContext;
    App = app;
    Context = rootContext = new ZTestRootContext(App, app.GetLoggerForTestOutput(output));
      // , new ServiceCollection()
      // .AddZApp<TA, ZTestRootContext>(App)
      // .BuildServiceProvider());

    ZEnv.SetRootContextSpawner(() => rootContext);
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

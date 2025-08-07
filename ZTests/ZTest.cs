#region

using IZ.Core;
using IZ.Core.Contexts;
using Xunit.Abstractions;

#endregion

namespace ZTests;

public abstract class ZTest<TA> : LogicBase where TA : ZTestApp {

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

  protected TA App { get; }

  // Where the test project lives
  protected virtual string TestProjectDir => Path.Combine("..", "..", "..");

  // If part of solution, overwrite, preferring git path as root
  protected virtual string SolutionDir => TestProjectDir;

  protected virtual string UserDir => Path.Combine(MonoRepoRoot, "User");

  protected string MonoRepoRoot => SolutionDir;


  protected override string ContextualObjectGroup => "Test";

  ~ZTest() {
    // Root.Log.Information("[TEST] shutting down...");
    // TuneConfig.DatadogLogSink!.DisposeAsync().ConfigureAwait(true);
    Task.Delay(15000).Wait();
    // Root.Log.Information("[TEST] done");
  }
}

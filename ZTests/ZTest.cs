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

  // No finalizer. One used to block 15 s (a leftover from letting a Datadog batching sink drain
  // before a test process ended): .NET Core never runs finalizers at exit, so it drained nothing,
  // and it parked the finalizer thread for 15 s per test instance — a crash dump from the Shpkpr
  // CI test host showed the finalizer thread sitting in it.
}

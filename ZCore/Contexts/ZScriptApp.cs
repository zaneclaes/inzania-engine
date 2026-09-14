#region

using System;
using System.IO;
using IZ.Core.Observability.Logging;
using IZ.Core.Utils;

#endregion

namespace IZ.Core.Contexts;

/// <summary>
/// The app a standalone tool runs under: a .NET file-based script (`dotnet run X.cs`) such as a Claude
/// hook, an installer or a CI step. It exists so those scripts use the engine's own services, above all
/// `ZJson`, the only JSON serializer the codebase allows (`.claude/hooks/JsonGuard.cs`), which needs a
/// `ZEnv.App` to build its context. A script references `ZCore` and starts one first:
/// <code>
/// #:project ../../ZCore/ZCore.csproj        // relative to the script
/// ZScriptApp.Start("JsonGuard");
/// </code>
/// It has no settings, storage or services. Its log goes to stderr at Warning and above, because a
/// script's stdout is its output (a hook's verdict, JSON another tool reads) and must stay clean.
/// </summary>
public sealed class ZScriptApp : ZApp {
  private ZScriptApp(string name) : base(
    name, "localhost",
    _ => ZTask<IZAppSettings>.FromResult(null!),
    () => EmptyServices.Instance,
    ZEnvironment.Development, null, ZTarget.CI
  ) { }

  /// <summary>Creates the script's app and makes it `ZEnv.App`. Call once, before any `ZJson` use.</summary>
  public static ZScriptApp Start(string name, TextWriter? log = null, ZEventLevel minimumLevel = ZEventLevel.Warning) {
    ZEnv.Log = new ConsoleLogger(log ?? Console.Error, minimumLevel);
    return new ZScriptApp(name);
  }

  private sealed class EmptyServices : IServiceProvider {
    public static readonly EmptyServices Instance = new EmptyServices();
    public object? GetService(Type serviceType) => null;
  }
}

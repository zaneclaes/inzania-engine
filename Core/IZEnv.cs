using System;
using System.Diagnostics;
using IZ.Core.Contexts;
using IZ.Core.Observability.Logging;
using IZ.Core.Utils;

namespace IZ.Core;

public enum TuneTarget {
  UnitTests = -10,
  CI = -6,
  InternalApp = -5,
  Server = 0,
  PublicApp = 1
}

public enum IZEnvironment {
  Testing = -10, // Unit tests
  Internal = -5, // i.e., scraper app
  Development = 0, // Development target of public / general app
  Staging = 5,
  Production = 10
}

public static class IZEnv {
  public static string ProductName => App.ProductName;

  public static string DomainName => App.DomainName;
  
  public static Func<ITuneContext, ITuneSpan> SpanBuilder { get; set; } = TuneSpan.ForContext;

  public static DateTime Now => DateTime.UtcNow;

  public static ITuneLogger Log { get; set; } = new ConsoleLogger();

  public static ZApp App { get; internal set; } = default!;

  public static ITuneRootContext SpawnRootContext() {
    // Log.Information("[ROOT] {stack}", new TuneTrace(new StackTrace().ToString()).ToString());
    return _defaultContextBuilder?.Invoke() ??
      throw new SystemException("IZEnv defaultContextBuilder does not exist");
  }
  public static void SetRootContextSpawner(Func<ITuneRootContext> contextBuilder) {
    _defaultContextBuilder = contextBuilder;
  }
  private static Func<ITuneRootContext>? _defaultContextBuilder;

  public static string SerializeTuneEnum<T>(this T e) where T : Enum => e.ToString().ToSnakeCase().ToUpper();

  public static string ToShortString(this IZEnvironment e) {
    if (e == IZEnvironment.Testing) return "test";
    if (e == IZEnvironment.Development) return "dev";
    if (e == IZEnvironment.Production) return "prod";
    return e.ToString().ToLower();
  }
}

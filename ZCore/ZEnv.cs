using System;
using System.Diagnostics;
using IZ.Core.Contexts;
using IZ.Core.Observability.Logging;
using IZ.Core.Utils;

namespace IZ.Core;

public enum ZTarget {
  UnitTests = -10,
  CI = -6,
  InternalApp = -5,
  Server = 0,
  PublicApp = 1
}

public enum ZEnvironment {
  Testing = -10, // Unit tests
  Internal = -5, // i.e., scraper app
  Development = 0, // Development target of public / general app
  Staging = 5,
  Production = 10
}

public static class ZEnv {
  public static string ProductName => App.ProductName;

  public static string DomainName => App.DomainName;
  
  public static Func<IZContext, IZSpan> SpanBuilder { get; set; } = ZSpan.ForContext;

  public static DateTime Now => DateTime.UtcNow;

  public static IZLogger Log { get; set; } = new ConsoleLogger();

  public static ZApp App { get; internal set; } = null!;

  public static IZRootContext SpawnRootContext() {
    return _defaultContextBuilder?.Invoke() ??
      throw new SystemException("ZEnv defaultContextBuilder does not exist");
  }
  public static void SetRootContextSpawner(Func<IZRootContext> contextBuilder) {
    _defaultContextBuilder = contextBuilder;
  }
  private static Func<IZRootContext>? _defaultContextBuilder;

  public static string SerializeZEnum<T>(this T e) where T : Enum => e.ToString().ToSnakeCase().ToUpper();

  public static string ToShortString(this ZEnvironment e) {
    if (e == ZEnvironment.Testing) return "test";
    if (e == ZEnvironment.Development) return "dev";
    if (e == ZEnvironment.Production) return "prod";
    return e.ToString().ToLower();
  }
}

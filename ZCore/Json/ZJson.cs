#region

using System;
using IZ.Core.Contexts;
using IZ.Core.Json.System;

#endregion

namespace IZ.Core.Json;

public class ZJsonSerializationOpts {
  public bool PrettyPrint { get; set; }

  public bool IgnoreDefaults { get; set; } = true;

  public bool IgnoreNull { get; set; } = true;

  public string? ApiFormat { get; set; }
}

public static class ZJson {
  private static IZContext? _defaultContext;
  public static IZContext DefaultContext {
    get => _defaultContext ??= new WorkContext(ZEnv.App);
    set => _defaultContext = value;
  }

  public static ZJsonSerializationOpts DefaultOptions { get; set; } = new ZJsonSerializationOpts();

  public static IZJson Converter { get; set; } = new SystemJson();

  public static string SerializeObject<TObj>(TObj obj, ZJsonSerializationOpts? opts = null) =>
    Converter.SerializeObject(obj, opts);

  /// <summary>
  /// Pretty-prints without touching the caller's options — or, worse, the shared ones. This used to
  /// do `opts ??= DefaultOptions; opts.PrettyPrint = true;`, so a single pretty-print anywhere in the
  /// process turned indentation on for every later `SerializeObject` that fell through to the
  /// defaults. That is invisible until something writes a committed file, at which point whether the
  /// artifact is indented depends on what ran before it.
  /// </summary>
  public static string PrettyPrintObject<TObj>(TObj obj, ZJsonSerializationOpts? opts = null) {
    var src = opts ?? DefaultOptions;
    return SerializeObject(obj, new ZJsonSerializationOpts {
      PrettyPrint = true,
      IgnoreDefaults = src.IgnoreDefaults,
      IgnoreNull = src.IgnoreNull,
      ApiFormat = src.ApiFormat,
    });
  }

  public static TObj? DeserializeObject<TObj>(IZContext? context, string str) =>
    (TObj?) Converter.DeserializeObject(context ?? DefaultContext, str, typeof(TObj));

  public static TObj? DeserializeObject<TObj>(string str) => DeserializeObject<TObj>(DefaultContext, str);

  public static object? DeserializeObject(IZContext? context, string str, Type t) =>
    Converter.DeserializeObject(context ?? DefaultContext, str, t);

  public static object? DeserializeObject(string str, Type t) => DeserializeObject(DefaultContext, str, t);
}

#region

using System;
using IZ.Core.Contexts;
using IZ.Core.Json.System;

#endregion

namespace IZ.Core.Json;

public class ZJsonSerializationOpts {
  public bool PrettyPrint { get; set; }
}

public static class ZJson {
  private static IZContext? _defaultContext;
  public static IZContext DefaultContext {
    get => _defaultContext ??= new WorkContext(ZEnv.App);
    set => _defaultContext = value;
  }

  public static IZJson Converter { get; set; } = new SystemJson();

  public static string SerializeObject<TObj>(TObj obj, ZJsonSerializationOpts? opts = null) =>
    Converter.SerializeObject(obj, opts);

  public static string PrettyPrintObject<TObj>(TObj obj, ZJsonSerializationOpts? opts = null) {
    opts ??= new ZJsonSerializationOpts();
    opts.PrettyPrint = true;
    return SerializeObject(obj, opts);
  }

  public static TObj? DeserializeObject<TObj>(IZContext? context, string str) =>
    (TObj?) Converter.DeserializeObject(context ?? DefaultContext, str, typeof(TObj));

  public static TObj? DeserializeObject<TObj>(string str) => DeserializeObject<TObj>(DefaultContext, str);

  public static object? DeserializeObject(IZContext? context, string str, Type t) =>
    Converter.DeserializeObject(context ?? DefaultContext, str, t);

  public static object? DeserializeObject(string str, Type t) => DeserializeObject(DefaultContext, str, t);
}

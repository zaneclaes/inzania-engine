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
  public static IZJson Converter { get; set; } = new SystemJson();

  public static string SerializeObject<TObj>(TObj obj, ZJsonSerializationOpts? opts = null) =>
    Converter.SerializeObject(obj, opts);

  public static TObj? DeserializeObject<TObj>(IZContext context, string str) =>
    (TObj?) Converter.DeserializeObject(context, str, typeof(TObj));

  public static object? DeserializeObject(IZContext context, string str, Type t) =>
    Converter.DeserializeObject(context, str, t);
}

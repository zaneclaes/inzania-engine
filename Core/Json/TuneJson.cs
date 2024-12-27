#region

using System;
using IZ.Core.Contexts;
using IZ.Core.Json.System;

#endregion

namespace IZ.Core.Json;

public class TuneJsonSerializationOpts {
  public bool PrettyPrint { get; set; }
}

public static class TuneJson {
  public static ITuneJson Converter { get; set; } = new SystemJson();

  public static string SerializeObject<TObj>(TObj obj, TuneJsonSerializationOpts? opts = null) =>
    Converter.SerializeObject(obj, opts);

  public static TObj? DeserializeObject<TObj>(ITuneContext context, string str) =>
    (TObj?) Converter.DeserializeObject(context, str, typeof(TObj));

  public static object? DeserializeObject(ITuneContext context, string str, Type t) =>
    Converter.DeserializeObject(context, str, t);
}

#region

using System;
using IZ.Core.Contexts;

#endregion

namespace IZ.Core.Json;

public interface ITuneJson {
  public string SerializeObject<TObj>(TObj obj, TuneJsonSerializationOpts? opts = null);

  public object? DeserializeObject(ITuneContext context, string str, Type t);

  // public TObj? DeserializeObject<TObj>(ITuneContext context, string str);
}

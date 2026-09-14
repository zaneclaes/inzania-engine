#region

using System;
using IZ.Core.Contexts;

#endregion

namespace IZ.Core.Json;

public interface IZJson {
  public string SerializeObject<TObj>(TObj obj, ZJsonSerializationOpts? opts = null);

  public object? DeserializeObject(IZContext context, string str, Type t, ZJsonSerializationOpts? opts = null);

  // public TObj? DeserializeObject<TObj>(IZContext context, string str);
}

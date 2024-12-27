#region

using System.Text.Json;
using IZ.Core;
using IZ.Core.Contexts;
using IZ.Core.Utils;

#endregion


namespace IZ.Client.Queries;

public class EnumNamingPolicy : JsonNamingPolicy {
  public sealed override string ConvertName(string name) {
    IZEnv.Log.Information("[name] {name} -> {tc}", name, name.ToSnakeCase().ToUpper());
    return name.ToSnakeCase().ToUpper();
  }
}

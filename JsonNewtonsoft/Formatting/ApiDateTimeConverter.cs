#region

using System;
using IZ.Core;
using IZ.Core.Contexts;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

#endregion

namespace IZ.Json.Newtonsoft.Formatting;

public class ApiDateTimeConvertor : DateTimeConverterBase {
  public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer) {
    string? val = reader.Value?.ToString();
    if (string.IsNullOrWhiteSpace(val)) return null;

    if (!val.EndsWith("Z")) {
      int li = val.LastIndexOf("-", StringComparison.InvariantCulture);
      if (li > 0) val = val.Substring(0, li) + "Z";
    }

    if (!DateTime.TryParse(val, out var ret)) {
      IZEnv.Log.Warning("[DATE] failed to convert {val} to DateTime", val);
      return null;
    }
    ret = ret.ToUniversalTime();
    IZEnv.Log.Debug("[DATE] {val} => {ret}", val, ret);
    return ret;
  }

  public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer) {
    writer.WriteValue(((DateTime) value!).ToString("dd/MM/yyyy hh:mm"));
  }
}

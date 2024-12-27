#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using IZ.Core.Contexts;
using IZ.Core.Utils;

#endregion

namespace IZ.Core.Json.System;

public class EnumConverter<TEnum> : JsonConverter<TEnum> where TEnum : struct, Enum {

  public override TEnum Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
    if (reader.TokenType == JsonTokenType.Number) return (TEnum) (object) reader.GetInt32();
    if (reader.TokenType != JsonTokenType.String) throw new ArgumentException($"Convert {reader.TokenType} to enum");
    string? val = reader.GetString();
    return val == null ? throw new NullReferenceException(nameof(val)) :
      Enum.Parse<TEnum>(string.Join("", val.Split("_").Select(s => s[0] + s.Substring(1).ToLower())));
  }

  public static string Get(TEnum value) => value.ToString().ToSnakeCase().ToUpper();

  public override void Write(Utf8JsonWriter writer, TEnum value, JsonSerializerOptions options) {
    writer.WriteStringValue(Get(value));
  }
}

public class ListEnumConverter<TEnum> : JsonConverter<List<TEnum>> where TEnum : struct, Enum {
  private EnumConverter<TEnum> _enumConverter = new EnumConverter<TEnum>();

  public override List<TEnum> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
    List<TEnum> ret = new List<TEnum>();
    while (reader.TokenType != JsonTokenType.EndArray) {
      IZEnv.Log.Information("READ {token}", reader.TokenType);
      // ret.Add(_enumConverter.Read(ref reader, typeToConvert, options));
      if (!reader.Read()) break;
    }
    return ret;
  }

  public override void Write(Utf8JsonWriter writer, List<TEnum> value, JsonSerializerOptions options) {
    string? contents = value.Any() ? "\"" + string.Join("\",\"", value.Select(EnumConverter<TEnum>.Get)) + "\"" : "";
    writer.WriteRawValue("[" + contents + "]");
  }
}

public class NullableEnumConverter<TEnum> : JsonConverter<TEnum?> where TEnum : struct, Enum {
  public override TEnum? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
    if (reader.TokenType == JsonTokenType.Number) return (TEnum) (object) reader.GetInt32();
    if (reader.TokenType != JsonTokenType.String) throw new ArgumentException($"Convert {reader.TokenType} to enum");
    string? val = reader.GetString();
    return string.IsNullOrWhiteSpace(val) ? null :
      Enum.Parse<TEnum>(string.Join("", val.Split("_").Select(s => s[0] + s.Substring(1).ToLower())));
  }

  public override void Write(Utf8JsonWriter writer, TEnum? value, JsonSerializerOptions options) {
    if (value == null) writer.WriteNullValue();
    else writer.WriteStringValue(value.Value.SerializeTuneEnum());
  }
}

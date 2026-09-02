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

/// <summary>
/// Enums cross the wire SCREAMING_SNAKE (<see cref="ZEnv.SerializeZEnum{T}" />). Reading is delegated
/// to <see cref="ZEnums" />, which is built from that same policy and degrades an unrecognized value to
/// the type's `Unknown` rather than throwing — see the note there for why the alternative makes every
/// added enum value a breaking change for every client already in the wild.
/// </summary>
public class EnumConverter<TEnum> : JsonConverter<TEnum> where TEnum : struct, Enum {

  public override TEnum Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
    if (reader.TokenType == JsonTokenType.Number) return (TEnum) ZEnums.FromNumber(typeof(TEnum), reader.GetInt64());
    if (reader.TokenType == JsonTokenType.Null) return ZEnums.Fallback<TEnum>();
    if (reader.TokenType != JsonTokenType.String) throw new ArgumentException($"Convert {reader.TokenType} to enum");
    return ZEnums.Parse<TEnum>(reader.GetString());
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
      ZEnv.Log.Information("READ {token}", reader.TokenType);
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
  // A nullable enum has somewhere better than `Unknown` to put a value this build does not know, so
  // both an unrecognized name and an unrecognized ordinal read back as null rather than as a value
  // the caller would mistake for a real one.
  public override TEnum? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
    if (reader.TokenType == JsonTokenType.Null) return null;
    if (reader.TokenType == JsonTokenType.Number)
      return ZEnums.TryParse(reader.GetInt64().ToString(), out TEnum num) ? num : (TEnum?) null;
    if (reader.TokenType != JsonTokenType.String) throw new ArgumentException($"Convert {reader.TokenType} to enum");
    string? val = reader.GetString();
    return string.IsNullOrWhiteSpace(val) ? null :
      ZEnums.TryParse(val, out TEnum parsed) ? parsed : (TEnum?) null;
  }

  public override void Write(Utf8JsonWriter writer, TEnum? value, JsonSerializerOptions options) {
    if (value == null) writer.WriteNullValue();
    else writer.WriteStringValue(value.Value.SerializeZEnum());
  }
}

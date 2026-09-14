#region

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

#endregion

namespace IZ.Core.Json.System;

/// <summary>
/// Reads a value typed `object` as plain .NET values rather than a serializer DOM: an object becomes a
/// `Dictionary&lt;string, object?&gt;`, an array a `List&lt;object?&gt;`, a number a `long` (or a `double`), and strings,
/// booleans and null themselves. It is how code handles JSON whose shape it does not know — a JSON-LD graph, a
/// third-party payload it only partly reads — through `ZJson` alone
/// (`ZJsonSerializationOpts.ObjectsAsDictionaries`). Writing needs nothing special: dictionaries and lists
/// serialize as themselves.
/// </summary>
public sealed class InferredObjectConverter : JsonConverter<object> {
  public override object? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
    switch (reader.TokenType) {
      case JsonTokenType.True: return true;
      case JsonTokenType.False: return false;
      case JsonTokenType.Null: return null;
      case JsonTokenType.String: return reader.GetString();
      case JsonTokenType.Number:
        // Two returns, not `cond ? l : GetDouble()`: a conditional unifies to double and would box 12 as 12.0.
        if (reader.TryGetInt64(out long l)) return l;
        return reader.GetDouble();
      case JsonTokenType.StartArray: {
        var list = new List<object?>();
        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray) list.Add(Read(ref reader, typeof(object), options));
        return list;
      }
      case JsonTokenType.StartObject: {
        var dict = new Dictionary<string, object?>();
        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject) {
          string key = reader.GetString() ?? "";
          reader.Read();
          dict[key] = Read(ref reader, typeof(object), options);
        }
        return dict;
      }
      default: throw new JsonException($"Unexpected {reader.TokenType} reading an object");
    }
  }

  public override void Write(Utf8JsonWriter writer, object value, JsonSerializerOptions options) {
    if (value.GetType() == typeof(object)) {
      writer.WriteStartObject();
      writer.WriteEndObject();
      return;
    }
    JsonSerializer.Serialize(writer, value, value.GetType(), options);
  }
}

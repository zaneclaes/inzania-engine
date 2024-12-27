#region

using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using IZ.Core.Api;
using IZ.Core.Api.Types;
using IZ.Core.Contexts;
using IZ.Core.Observability.Logging;
using IZ.Core.Utils;

#endregion

namespace IZ.Core.Json.System;

public class TuneArrayConverter<TObj> : JsonConverter<TObj[]> {
  private readonly JsonConverter<object> _converter;

  public TuneArrayConverter(JsonConverter<object> inner) {
    _converter = inner;
  }

  public override TObj[]? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
    var type = typeToConvert.GetElementType()!;
    if (reader.TokenType == JsonTokenType.StartArray) reader.Read();
    List<TObj> objects = new List<TObj>();
    while (reader.TokenType != JsonTokenType.EndArray) {
      var obj = (TObj?) _converter.Read(ref reader, type, options);
      if (obj != null) objects.Add(obj);
    }
    reader.Read();
    return objects.ToArray();
  }

  public override void Write(Utf8JsonWriter writer, TObj[] value, JsonSerializerOptions options) {
    JsonSerializer.Serialize(writer, value, value.GetType(), SystemJson.DeserializeOptionsForContext(null));
  }
}

public class TuneContextConverter : JsonConverter<object>, IHaveContext {
  public ITuneContext Context { get; }
  public ITuneLogger Log { get; }

  public TuneContextConverter(ITuneContext context) {
    Context = context;
    Log = context.Log.ForContext(GetType());
  }

  private object ReadArray(ref Utf8JsonReader reader, Type type) {
    var list = (IList) Activator.CreateInstance(type)!; // typeof(List<>).MakeGenericType(type)
    // Context.Log.Information("ARR {key} {type}", reader.TokenType, type);
    var typeDescriptor = TuneTypeDescriptor.FromType(type);
    if (reader.TokenType == JsonTokenType.StartArray) reader.Read();
    while (reader.TokenType != JsonTokenType.EndArray) {
      if (reader.TokenType == JsonTokenType.StartObject) {
        list.Add(ReadObject(ref reader, typeDescriptor.ObjectDescriptor.ObjectType));
      } else {
        list.Add(JsonSerializer.Deserialize(ref reader, typeDescriptor.ObjectDescriptor.ObjectType, SystemJson.DeserializeOptionsForContext(Context)));
        reader.Read();
      }
    }
    reader.Read();
    // Context.Log.Information("ARR END {key} {type}", reader.TokenType, type);
    return list;
  }

  private object? ReadObject(ref Utf8JsonReader reader, Type type) {
    object? ret = Activator.CreateInstance(type);
    if (ret == null) return null;
    // Context.Log.Information("OBJ {key} {type}", reader.TokenType, type);
    var co = ret as ContextualObject;
    // if (!(ret is ContextualObject co)) throw new ArgumentException($"Read non-object {type}");
    if (co != null) co.Context = Context;
    var typeDescriptor = TuneTypeDescriptor.FromType(type);

    if (reader.TokenType == JsonTokenType.StartObject) reader.Read();

    while (reader.TokenType != JsonTokenType.EndObject) {
      string? propName = reader.GetString()!;
      string? fieldName = propName.ToFieldName();
      reader.Read();
      var prop = typeDescriptor.ObjectDescriptor.GetProperty(fieldName) ??
                 throw new SystemException($"Invalid JSON prop '{propName}' on {type}");
      // Context.Log.Information("{key} ({val}) = {prop}", propName, reader.TokenType, prop);

      object? val = null;
      if (reader.TokenType == JsonTokenType.StartObject) {
        if (prop.FieldType.IsAssignableToBaseType(typeof(IDictionary))) {
          Log.Warning("PROP {p} ON {type} INVALID", prop, type);
          while (reader.TokenType != JsonTokenType.EndObject) reader.Read();
          reader.Read();
        } else {
          val = ReadObject(ref reader, prop.FieldType);
        }
      } else if (reader.TokenType == JsonTokenType.StartArray) {
        val = ReadArray(ref reader, prop.FieldType);
      } else { // Scalars
        if (!prop.IsJsonIgnored) {
          if (reader.TokenType != JsonTokenType.Number && reader.TokenType != JsonTokenType.String && reader.TokenType != JsonTokenType.Null
              && reader.TokenType != JsonTokenType.True && reader.TokenType != JsonTokenType.False)
            Context.Log.Warning("READ JSON {token}", reader.TokenType);
          if (reader.TokenType != JsonTokenType.Null)
            val = JsonSerializer.Deserialize(ref reader, prop.FieldType, SystemJson.DeserializeOptionsForContext(Context)); // reader.GetDouble();
        }
        reader.Read();
      }
      if (!prop.IsJsonIgnored) {
        // Context.Log.Information("{key} = {val} ({type}) on {ret}", propName, val,val?.GetType(),  ret.GetType());
        prop.SetValue(ret, val);
      } else {
        Context.Log.Warning("INVALID SET {key} = {val} ({type}) on {ret}", propName, val, val?.GetType(), ret.GetType());
      }
    }
    reader.Read();
    // if (reader.TokenType == JsonTokenType.EndArray) reader.Read();
    // Context.Log.Information("OBJ END {key} {type}", reader.TokenType, type);

    return co;
  }

  public override object? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
    // reader.TokenType == JsonTokenType.StartArray ? ReadArray(ref reader, typeof(Array).MakeGenericType(typeToConvert)) :
    ReadObject(ref reader, typeToConvert);

  public override void Write(Utf8JsonWriter writer, object value, JsonSerializerOptions options) {
    // TuneTypeDescriptor typeDescriptor = TuneTypeDescriptor.FromType(value.GetType());
    // string val = "{" + string.Join(",", typeDescriptor.ObjectDescriptor.AllProperties
    //   .Where(p => !p.IsJsonIgnored)
    //   .Select(p => $"\"{p.FieldName}\":{JsonSerializer.Serialize(p.GetValue(value), options)}")) + "}";
    // // JsonSerializer.Serialize(writer, value, value.GetType(), options);
    // writer.WriteRawValue(val);
    JsonSerializer.Serialize(writer, value, value.GetType(), SystemJson.DeserializeOptionsForContext(null));
  }
}

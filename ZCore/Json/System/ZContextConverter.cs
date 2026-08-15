#region

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using IZ.Core.Api;
using IZ.Core.Api.Types;
using IZ.Core.Contexts;
using IZ.Core.Data;
using IZ.Core.Observability.Logging;
using IZ.Core.Utils;

#endregion

namespace IZ.Core.Json.System;

public class ZArrayConverter<TObj> : JsonConverter<TObj[]>, IHaveContext {
  private readonly ZContextConverter _converter;
  private JsonSerializerOptions? _serializerOptions;

  public ZArrayConverter(ZContextConverter inner) {
    _converter = inner;
  }

  private JsonSerializerOptions SerializerOptions =>
    _serializerOptions ??= SystemJson.DeserializeOptionsForContext(null);
  public IZContext Context => _converter.Context;
  public IZLogger Log => _converter.Log;

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
    // Log.Information("[WRITE-ARR] {type}", value.GetType());
    // JsonSerializer.Serialize(writer, value, value.GetType(), SerializerOptions);
    throw new SystemException("ZArrayConverter cannot write");
  }
}

public class ZContextConverter : JsonConverter<object>, IHaveContext {
  private JsonSerializerOptions? _deserializerOptions;
  // private JsonSerializerOptions? _serializerOptions;
  private ZJsonSerializationOpts? _opts;

  public ZContextConverter(IZContext context, ZJsonSerializationOpts? opts = null) {
    Context = context;
    Log = context.Log.ForContext(GetType());
    _opts = opts ?? ZJson.DefaultOptions;
  }

  private JsonSerializerOptions DeserializerOptions =>
    _deserializerOptions ??= SystemJson.DeserializeOptionsForContext(Context, null);

  // private JsonSerializerOptions SerializerOptions =>
  //   _serializerOptions ??= SystemJson.DeserializeOptionsForContext(null, null);

  public IZContext Context { get; }
  public IZLogger Log { get; }

  private string[] AddBreadcrumb(string bc, params string[] bcs) {
    List<string> breadcrums = bcs.ToList();
    if (bcs.Any() && !bc.StartsWith("[")) bc = "." + bc;
    breadcrums.Add(bc);
    return breadcrums.ToArray();
  }

  private object? ReadArray(ref Utf8JsonReader reader, Type type, params string[] breadcrumbs) {
    Log.Debug("[JSON] ARR START {idx}", string.Join("", breadcrumbs));
    var list = (IList) Activator.CreateInstance(type)!; // typeof(List<>).MakeGenericType(type)
    // Context.Log.Debug("ARR {key} {type}", reader.TokenType, type);
    var typeDescriptor = ZApi.LoadTypeDescriptor(type);
    if (reader.TokenType == JsonTokenType.StartArray) reader.Read();
    int p = 0;
    while (reader.TokenType != JsonTokenType.EndArray) {
      Context.Log.Debug("[JSON] ARR ITEM {idx} {type}", string.Join("", breadcrumbs), reader.TokenType);
      if (reader.TokenType == JsonTokenType.StartObject) {
        list.Add(ReadObject(ref reader, typeDescriptor.ObjectDescriptor.ObjectType, AddBreadcrumb($"[{p}]", breadcrumbs)));
      } else {
        list.Add(JsonSerializer.Deserialize(ref reader, typeDescriptor.ObjectDescriptor.ObjectType, SystemJson.DeserializeOptionsForContext(Context)));
        reader.Read();
      }
      p++;
    }
    Context.Log.Debug("[JSON] ARR END 1 {idx} {key} {type}", string.Join("", breadcrumbs), reader.TokenType, type);
    reader.Read();
    Context.Log.Debug("[JSON] ARR END 2 {idx} {key} {type}", string.Join("", breadcrumbs), reader.TokenType, type);
    return list;
  }

  private Type? GetScalarType(JsonTokenType tokenType) {
    if (tokenType == JsonTokenType.Number) return typeof(decimal);
    if (tokenType == JsonTokenType.String) return typeof(decimal);
    if (tokenType == JsonTokenType.Null) return typeof(decimal);
    if (tokenType == JsonTokenType.True) return typeof(decimal);
    if (tokenType == JsonTokenType.False) return typeof(decimal);
    return null;
  }

  private IDictionary ReadDictionary(ref Utf8JsonReader reader, Type type, params string[] breadcrumbs) {
    var dict = (IDictionary) Activator.CreateInstance(type)!;
    var dictionaryInterface = type
      .GetInterfaces()
      .Append(type) // also check the type itself
      .FirstOrDefault(i =>
        i.IsGenericType &&
        i.GetGenericTypeDefinition() == typeof(IDictionary<,>)
      ) ?? throw new ArgumentException($"Type {type} does not implement IDictionary<,> or is not a generic type");
    Type[] generics = dictionaryInterface.GetGenericArguments();
    if (generics[0] != typeof(string)) throw new ArgumentException($"Type {type} does not implement IDictionary<string,>");
    var valueType = generics[1];

    if (reader.TokenType == JsonTokenType.StartObject) reader.Read();
    while (reader.TokenType != JsonTokenType.EndObject) {
      string key = reader.GetString() ?? throw new ArgumentException($"Read non-string key {reader.TokenType}");
      reader.Read();
      object? val = JsonSerializer.Deserialize(ref reader, valueType, DeserializerOptions); //eadObject(ref reader, valueType, AddBreadcrumb("value", breadcrumbs));
      reader.Read();
      dict.Add(key, val);
    }
    reader.Read();
    return dict;
  }

  private object? ReadObject(ref Utf8JsonReader reader, Type type, params string[] breadcrumbs) {
    Log.Verbose("[JSON] OBJ START {idx} {type} {token}", string.Join("", breadcrumbs), type, reader.TokenType);

    // GetPolymorphicType(ref reader);

    var vals = new Dictionary<ZPropertyDescriptor, object?>();
    var typeDescriptor = ZApi.LoadTypeDescriptor(type);
    var polymorphicDiscriminatorName = typeDescriptor.ObjectDescriptor.PolymorphicDiscriminatorName;

    if (reader.TokenType == JsonTokenType.StartObject) reader.Read();

    while (reader.TokenType != JsonTokenType.EndObject) {
      string propName = reader.GetString()!;
      // if (propName == null) {
      //   Log.Warning("READER broke on {tt}", reader.TokenType);
      //   reader.Read();
      //   continue;
      // }
      reader.Read();
      var prop = typeDescriptor.ObjectDescriptor.GetProperty(propName) ??
                 typeDescriptor.ObjectDescriptor.GetProperty(propName.ToFieldName());
      // Log.Information("[JSON] READ {type}.{field} {token}", type, fieldName, reader.TokenType);

      object? val = null;
      if (reader.TokenType == JsonTokenType.StartObject) {
        if (prop?.FieldType.IsAssignableToBaseType(typeof(IDictionary)) ?? false) {
          val = ReadDictionary(ref reader, prop.FieldType, AddBreadcrumb(propName, breadcrumbs));
        } else if (prop?.FieldType == typeof(string)) {
          val = reader.GetString();
        } else {
          bool isList = prop?.FieldType.IsListType() ?? false;
          var ft = isList ? prop!.FieldType.GetListType()! : prop?.FieldType ?? typeof(object);
          val = ReadObject(ref reader, ft, AddBreadcrumb(propName, breadcrumbs));

          if (isList) { // The server gave a single object, but the datatype is a list
            var list = (IList) Activator.CreateInstance(prop!.FieldType)!;
            list.Add(val);
            val = list;
            // Log.Information("[JSON] READ {type}.{field} now {type}", type, fieldName, prop.FieldType);
          }
        }
      } else if (reader.TokenType == JsonTokenType.StartArray) {
        val = ReadArray(ref reader, prop?.FieldType ?? typeof(List<object>), AddBreadcrumb(propName, breadcrumbs));
      } else { // Scalars
        var tt = reader.TokenType;
        if (prop != null) { //  && !prop.IsIgnoredForFormat(_opts?.ApiFormat)
          if (tt != JsonTokenType.Number && tt != JsonTokenType.String && tt != JsonTokenType.Null
              && tt != JsonTokenType.True && tt != JsonTokenType.False)
            Context.Log.Warning("[JSON] READ JSON {token}", reader.TokenType);
          if (tt != JsonTokenType.Null) {
            val = JsonSerializer.Deserialize(ref reader, prop.FieldType, DeserializerOptions); // reader.GetDouble();
          }
        }
        reader.Read();

        if (polymorphicDiscriminatorName != null && prop?.Name == polymorphicDiscriminatorName) {
          if (ZApi.TypeMap.ApiObjects.TryGetValue(val?.ToString() ?? "", out var td)) {
            if (td.ObjectType.HasAssignableType(typeDescriptor.ObjectDescriptor.ObjectType)) {
              typeDescriptor = ZApi.LoadTypeDescriptor(td.ObjectType);
            } else {
              Context.Log.Warning("[DISCRIMINATOR] {t} is not a subclass of {parent}", td, typeDescriptor);
            }
          } else {
            Context.Log.Warning("[DISCRIMINATOR] {v} could not be found among {types}", val, ZApi.TypeMap.ApiObjects.Keys);
          }
        }

        Context.Log.Verbose("[JSON] SET {key} = {val} ({prop}) o {r}", propName, val, prop == null || prop.IsIgnoredForFormat(_opts?.ApiFormat), tt);
      }
      if (prop != null) {
        if (prop.IsSettable) {
          // Context.Log.Information("{key} = {val} ({type}) on {r}", propName, val,val?.GetType());
          // prop.SetValue(ret, val);
          vals[prop] = val;
        } else {
          Context.Log.Warning("INVALID SET {key} = {val} ({type}) on {r}", propName, val, val?.GetType(), typeDescriptor);
        }
      } else {
        Context.Log.Verbose("[JSON] SET {key} MISSING FROM {desc} AMONG {fields}", propName, typeDescriptor.ObjectDescriptor, typeDescriptor.ObjectDescriptor.AllProperties.Select(p => p.FieldName));
      }
    }
    // Context.Log.Debug("OBJ END1 {idx} {key} {type}", n,reader.TokenType, type);
    reader.Read();
    // if (reader.TokenType == JsonTokenType.EndArray) reader.Read();
    Context.Log.Debug("[JSON] OBJ END2 {idx} {key} {type}", string.Join("", breadcrumbs), reader.TokenType, typeDescriptor);

    object ret = Activator.CreateInstance(typeDescriptor.OrigType)!;
    foreach (var k in vals.Keys) {
      k.SetValue(ret, vals[k]);
    }
    if (ret is ContextualObject contextualObject) contextualObject.OnDeserialized(Context);
    return ret;
  }

  public override object? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
    if (reader.TokenType == JsonTokenType.StartArray) {
      Log.Debug("ARR {type} {t2}", typeToConvert, typeToConvert.GenericTypeArguments);
      object? ret = ReadArray(ref reader, typeToConvert, ModelId.GenerateId());
      Log.Debug("ARR RET {ret} {type} {token}", ret, ret?.GetType(), reader.TokenType);
      return ret;
    }
    return ReadObject(ref reader, typeToConvert, ModelId.GenerateId());
  }

  public override void Write(Utf8JsonWriter writer, object value, JsonSerializerOptions options) {
    if (value is IEnumerable enumerable and not string) {
      writer.WriteStartArray();
      foreach (object? item in enumerable) {
        Write(writer, item, options);
      }
      writer.WriteEndArray();
      return;
    }

    if (value is IAmInternal) {
      // NO-OP
    } else if (value.GetType().IsScalar(false)) {
      JsonSerializer.Serialize(writer, value, value.GetType(), options); // SerializerOptions??
    } else {
      var desc = ZApi.LoadObjectDescriptor(value.GetType());
      writer.WriteStartObject();
      var props = desc.GetPropertiesForFormat(_opts?.ApiFormat);
      foreach (var prop in props) {
        object? val = prop.GetValue(value);
        if ((_opts?.IgnoreDefaults ?? true) && (val == prop.DefaultValue || (val?.Equals(prop.DefaultValue) ?? false))) continue;
        if ((_opts?.IgnoreNull ?? true) && (val == null)) continue;
        writer.WritePropertyName(prop.FieldName);
        if (val == null) writer.WriteNullValue();
        else Write(writer, val, options);
      }
      writer.WriteEndObject();
    }
  }
}

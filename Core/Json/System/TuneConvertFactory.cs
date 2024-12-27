#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using IZ.Core.Contexts;
using IZ.Core.Observability.Logging;
using IZ.Core.Utils;

#endregion

namespace IZ.Core.Json.System;

public class EnumConvertFactory : JsonConverterFactory {
  public override bool CanConvert(Type typeToConvert) => typeToConvert.HasAssignableType(typeof(Enum)) && typeToConvert != typeof(Enum);

  private JsonConverter Create(Type generic, Type type) =>
    (Activator.CreateInstance(generic.MakeGenericType(type)) as JsonConverter)!;

  public override JsonConverter? CreateConverter(Type typeToConvert, JsonSerializerOptions options) {
    try {
      if (Nullable.GetUnderlyingType(typeToConvert) != null) {
        return Create(typeof(NullableEnumConverter<>), typeToConvert.GetGenericArguments().First());
      }
      if (typeToConvert.GenericTypeArguments.Any()) {
        return Create(typeof(ListEnumConverter<>), typeToConvert.GetGenericArguments().First());
      }
      return Create(typeof(EnumConverter<>), typeToConvert);
    } catch (Exception e) {
      IZEnv.Log.Error(e, "Failed to convert enum {type}", typeToConvert);
      throw;
    }
  }
}

public class TuneConvertFactory : EnumConvertFactory, IHaveContext {
  public ITuneContext Context { get; }
  public ITuneLogger Log { get; }

  private readonly TuneContextConverter _contextConverter;
  private readonly Dictionary<Type, JsonConverter> _arrayConverters = new Dictionary<Type, JsonConverter>();

  public TuneConvertFactory(ITuneContext context) {
    Context = context;
    _contextConverter = new TuneContextConverter(context);
    Log = context.Log.ForContext(GetType());
  }

  private JsonConverter GetArrayConverter(Type t) {
    if (_arrayConverters.TryGetValue(t, out var converter)) return converter;
    return _arrayConverters[t] = (JsonConverter) Activator.CreateInstance(typeof(TuneArrayConverter<>).MakeGenericType(t), _contextConverter)!;
  }

  public override bool CanConvert(Type typeToConvert) {
    if (typeToConvert.IsArray) return CanConvert(typeToConvert.GetElementType()!);
    return base.CanConvert(typeToConvert) || typeToConvert.HasAssignableType<ContextualObject>();
  }

  public override JsonConverter? CreateConverter(Type typeToConvert, JsonSerializerOptions options) {
    if (typeToConvert.IsArray) {
      var innerType = typeToConvert.GetElementType()!;
      if (innerType.HasAssignableType<ContextualObject>()) {
        return GetArrayConverter(innerType);
      }
    }
    if (typeToConvert.HasAssignableType<ContextualObject>()) {
      return _contextConverter;
    }
    return base.CreateConverter(typeToConvert, options);
  }
}

#region

using System;
using System.Collections;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using IZ.Core.Contexts;

#endregion

namespace IZ.Core.Json.System;

public class SystemJson : ITuneJson {
  public static JsonSerializerOptions DeserializeOptionsForContext(ITuneContext? context) => new JsonSerializerOptions {
    PropertyNameCaseInsensitive = true,
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    NumberHandling = JsonNumberHandling.AllowReadingFromString,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    IgnoreReadOnlyFields = true,
    IgnoreReadOnlyProperties = true,
#if !TUNE_UNITY
    TypeInfoResolver = new DefaultJsonTypeInfoResolver {
      Modifiers = {
        DefaultValueModifier
      }
    },
#endif
    Converters = {
      new BoolConverter(),
      context == null ? new EnumConvertFactory() : new TuneConvertFactory(context)
    }
  };

  public string SerializeObject<TObj>(TObj obj, TuneJsonSerializationOpts? opts = null) {
    var context = obj is IHaveContext hc ? hc.Context : null;
    var o = DeserializeOptionsForContext(context);
    if (opts != null) {
      o.WriteIndented = opts.PrettyPrint;
    }
    return JsonSerializer.Serialize(obj, o);
  }

#if !TUNE_UNITY
  // Exclude empty arrays from response
  private static void DefaultValueModifier(JsonTypeInfo typeInfo) {
    foreach (var property in typeInfo.Properties) {
      if (typeof(ICollection).IsAssignableFrom(property.PropertyType)) {
        property.ShouldSerialize = (_, val) => val is ICollection collection && collection.Count > 0;
      }
    }
  }
#endif

  public object? DeserializeObject(ITuneContext context, string str, Type t) => JsonSerializer.Deserialize(str, t, DeserializeOptionsForContext(context));
}

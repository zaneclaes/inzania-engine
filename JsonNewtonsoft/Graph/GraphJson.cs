#region

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using IZ.Core.Contexts;
using IZ.Core.Exceptions;
using IZ.Json.Newtonsoft.Formatting;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

#endregion

namespace IZ.Json.Newtonsoft.Graph;

public class GraphJson {
  public static string graphQlTypenameKey = "__typename";

  public static string jsonTypenameKey = "$type";

  private static JsonSerializerSettings? _jsonSerializer;

  public static JsonSerializerSettings SerializationSettings { get; } = new JsonSerializerSettings {
    Converters = new List<JsonConverter> {
      new EnumInputConverter()
    },
    DateTimeZoneHandling = DateTimeZoneHandling.Utc
  };


  public static JsonSerializerSettings DeserializationSettings {
    get {
      _jsonSerializer ??= new JsonSerializerSettings {
        Converters = new List<JsonConverter> {
          new EnumInputConverter()
        },
        DateTimeZoneHandling = DateTimeZoneHandling.Utc
      };
#if UNITY_EDITOR
        // Developer server can be wonky with MySQL timezone timeztamps
        _jsonSerializer.DateParseHandling = DateParseHandling.None;
        _jsonSerializer.Converters.Add(new ApiDateTimeConvertor());
#endif
      return _jsonSerializer;
    }
  }


  public static IList ConvertArray(ITuneContext context, Type innerType, JArray objs) {
    IList arr = Array.CreateInstance(innerType, objs.Count);
    for (int i = 0; i < objs.Count; i++) arr[i] = ConvertObject(context, innerType, objs[i]);
    return arr;
  }

  public static object? ConvertObject(ITuneContext context, Type expectedType, JToken? token) {
    if (token == null || token.ToString().ToLowerInvariant().Equals("null")) return null;

    var realType = expectedType;
    try {
      if (expectedType.Name.EndsWith("[]")) {
        if (!(token is JArray arr)) throw new InternalTuneException(context, $"Type {expectedType.Name} did not provide array");
        return ConvertArray(context, expectedType.GetElementType()!, arr);
      }

      var data = token as JObject ?? throw new InternalTuneException(context, $"Token {token.GetType().Name} is not object");
      object? ret = JsonConvert.DeserializeObject(data.ToString(), realType, DeserializationSettings);
      if (ret == null) context.Log.Warning("[JSON] failed to deserialize {type} from {data}", realType.Name, data.ToString());
      else context.Log.Debug("[JSON] {type}: {data}", realType.Name, data.ToString());
      return ret;
    } catch (Exception e) {
      context.Log.Error(e, "Failed to cast GraphQL JSON to {type}: {data}",
        realType?.Name, JsonConvert.SerializeObject(token, global::Newtonsoft.Json.Formatting.Indented));
      return null;
    }
  }

  public static object? FromPayload(ITuneContext context, Type expectedType, string? text, string? key = null) {
    if (string.IsNullOrWhiteSpace(text)) throw new RemoteTuneException(context, "HTTP no text returned");

    // https://stackoverflow.com/questions/9490345/json-net-change-type-field-to-another-name
    text = text.Replace($"\"{graphQlTypenameKey}\"", $"\"{jsonTypenameKey}\"");

    var payload = JsonConvert.DeserializeObject<JObject>(text, DeserializationSettings);

    if (payload == null) return null;
    var error = payload["errors"];
    if (error != null) {
      context.Log.Error(error.ToString());
      return null;
    }

    var data = payload.Value<JObject>("data");
    if (data == null) {
      context.Log.Error("No data object returned");
      return null;
    }

    key ??= data.Properties().First().Name;

    return ConvertObject(context, expectedType, data.Value<JToken>(key));
  }

  private static Type? MapType(string tn) =>
    // if (tn.Equals(nameof(ContainerItem))) return typeof(ContainerItem);
    // if (tn.Equals(nameof(ConsumableItem))) return typeof(ConsumableItem);
    // if (tn.Equals(nameof(EquipmentItem))) return typeof(EquipmentItem);
    // if (tn.Equals(nameof(MaterialItem))) return typeof(MaterialItem);
    // if (tn.Equals(nameof(LootItem))) return typeof(LootItem);
    // if (tn.Equals(nameof(SpecialItem))) return typeof(SpecialItem);
    // if (tn.Equals(nameof(FurAccount))) return typeof(FurAccount);
    // if (tn.Equals(nameof(FurScholar))) return typeof(FurScholar);
    // if (tn.Equals(nameof(FurScholarEmail))) return typeof(FurScholarEmail);
    // if (tn.Equals(nameof(FurScholarDiscord))) return typeof(FurScholarDiscord);
    null;
}

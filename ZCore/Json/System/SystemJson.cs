#region

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using IZ.Core.Contexts;
#if !Z_UNITY
using System.Text.Encodings.Web;
#endif

#endregion

namespace IZ.Core.Json.System;

public class SystemJson : IZJson {

  public string SerializeObject<TObj>(TObj obj, ZJsonSerializationOpts? opts = null) {
    var context = obj is IHaveContext hc ? hc.Context : null;
    var o = DeserializeOptionsForContext(context, opts);
    return JsonSerializer.Serialize(obj, o);
  }

  public object? DeserializeObject(IZContext context, string str, Type t, ZJsonSerializationOpts? opts = null) =>
    JsonSerializer.Deserialize(str, t, DeserializeOptionsForContext(context, opts));
  /// <summary>
  /// Options are built once per context and option set, then reused. They were rebuilt on every call, which throws
  /// away System.Text.Json's per-type metadata cache each time: fine for a request's few calls, ruinous for a
  /// rollup that reads a field out of every event row. Keyed weakly by context, so a request's context and its
  /// options are collected together.
  /// </summary>
  public static JsonSerializerOptions DeserializeOptionsForContext(IZContext? context, ZJsonSerializationOpts? opts = null) {
    var ctx = context ?? ZJson.DefaultContext;
    var byOpts = OptionsByContext.GetValue(ctx, _ => new ConcurrentDictionary<string, JsonSerializerOptions>());
    string key = opts == null ? "" :
      $"{opts.PrettyPrint}|{opts.IgnoreNull}|{opts.AllowCommentsAndTrailingCommas}|{opts.UnsafeRelaxedEscaping}|{opts.ObjectsAsDictionaries}|{opts.IgnoreDefaults}|{opts.ApiFormat}";
    return byOpts.GetOrAdd(key, _ => {
      var options = BuildOptions(ctx, opts);
      if (opts?.ObjectsAsDictionaries == true) options.Converters.Insert(0, new InferredObjectConverter());
      return options;
    });
  }

  private static readonly ConditionalWeakTable<IZContext, ConcurrentDictionary<string, JsonSerializerOptions>> OptionsByContext =
    new ConditionalWeakTable<IZContext, ConcurrentDictionary<string, JsonSerializerOptions>>();

  private static JsonSerializerOptions BuildOptions(IZContext? context, ZJsonSerializationOpts? opts) => new JsonSerializerOptions {
    PropertyNameCaseInsensitive = true,
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    NumberHandling = JsonNumberHandling.AllowReadingFromString,
    // Nulls are dropped unless the caller asked to keep them (`IgnoreNull = false`): a null can be an answer.
    DefaultIgnoreCondition = opts?.IgnoreNull == false ? JsonIgnoreCondition.Never : JsonIgnoreCondition.WhenWritingNull,
    ReadCommentHandling = opts?.AllowCommentsAndTrailingCommas == true ? JsonCommentHandling.Skip : JsonCommentHandling.Disallow,
    AllowTrailingCommas = opts?.AllowCommentsAndTrailingCommas == true,
    IgnoreReadOnlyFields = true,
    IgnoreReadOnlyProperties = true,
#if !Z_UNITY
    Encoder = opts?.UnsafeRelaxedEscaping == true ? JavaScriptEncoder.UnsafeRelaxedJsonEscaping : null,
    TypeInfoResolver = new DefaultJsonTypeInfoResolver {
      Modifiers = {
        DefaultValueModifier
      }
    },
#endif
    WriteIndented = opts?.PrettyPrint ?? false,
    Converters = {
      new BoolConverter(),
      new ZConvertFactory(context ?? ZJson.DefaultContext, opts)
    }
  };

#if !Z_UNITY
  // Exclude empty arrays from response
  private static void DefaultValueModifier(JsonTypeInfo typeInfo) {
    foreach (var property in typeInfo.Properties) {
      if (typeof(ICollection).IsAssignableFrom(property.PropertyType)) {
        property.ShouldSerialize = (_, val) => val is ICollection collection && collection.Count > 0;
      }
    }
  }
#endif
}

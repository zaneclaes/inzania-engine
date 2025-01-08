#region

using System;
using IZ.Core.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

#endregion

namespace IZ.Json.Newtonsoft.Formatting;

public class EnumInputConverter : StringEnumConverter {
  public EnumInputConverter() : base(typeof(SnakeCaseNamingStrategy)) { }

  public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer) {
    if (value == null) {
      writer.WriteNull();
    } else {
      var @enum = (Enum) value;
      string? enumText = @enum.ToString().ToSnakeCase(false);
      writer.WriteRawValue($"\"{enumText}\"");
    }
  }

// 
//     // public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer) {
//     //   try {
//     //     string enumVal = reader.ReadAsString()?.ToCamelCase() ?? "";
//     //     return Enum.Parse(objectType, enumVal);
//     //   } catch (Exception) {
//     //     return base.ReadJson(reader, objectType, existingValue, serializer);
//     //   }
//     // }
//
//     public override object? ReadJson(
//       JsonReader reader,
//       Type objectType,
//       object? existingValue,
//       JsonSerializer serializer)
//     {
//       bool flag = ReflectionUtils.IsNullableType(objectType);
//       Type type = flag ? Nullable.GetUnderlyingType(objectType) : objectType;
//       try
//       {
//         if (reader.TokenType == JsonToken.String) {
//           string str = reader.Value?.ToString();
//           return string.IsNullOrEmpty(str) & flag ? (object) null : EnumUtils.ParseEnum(type, this.NamingStrategy, str, !this.AllowIntegerValues);
//         } if (reader.TokenType == JsonToken.Integer) {
//           if (!this.AllowIntegerValues)
//             throw JsonSerializationException.Create(reader, "Integer value {0} is not allowed.".FormatWith((IFormatProvider) CultureInfo.InvariantCulture, reader.Value));
//           return ConvertUtils.ConvertOrCast(reader.Value, CultureInfo.InvariantCulture, type);
//         }
//       } catch (Exception ex) {
//         return base.ReadJson(reader, objectType, existingValue, serializer);
//       }
//     }
// #nullable disable
}

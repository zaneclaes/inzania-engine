using System.Text.Json.Serialization;
using IZ.Core.Data;

namespace IZ.Core.Navigation.SchemaOrg;

public class SchemaOfferItemJson : TransientObject {
  [JsonPropertyName("@type")] public string Type { get; set; } = null!;

  [JsonPropertyName("@id")] public string Id { get; set; } = null!;

  [JsonPropertyName("contentUrl")] public string ContentUrl { get; set; } = null!;

  [JsonPropertyName("name")] public string Name { get; set; } = null!;

  [JsonPropertyName("encodingFormat")] public string EncodingFormat { get; set; } = null!;

  [JsonPropertyName("contentSize")] public string ContentSize { get; set; } = null!;
}

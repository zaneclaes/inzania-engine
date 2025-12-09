using System.Text.Json.Serialization;
using IZ.Core.Data;

namespace IZ.Core.Navigation.SchemaOrg;

public class SchemaWebSiteJson : TransientObject {
  [JsonPropertyName("@type")] public string Type { get; set; } = null!;

  [JsonPropertyName("@id")] public string Id { get; set; } = null!;

  [JsonPropertyName("name")] public string Name { get; set; } = null!;

  [JsonPropertyName("url")] public string Url { get; set; } = null!;
}

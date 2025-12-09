using System.Text.Json.Serialization;
using IZ.Core.Data;

namespace IZ.Core.Navigation.SchemaOrg;

public class SchemaEntityJson : TransientObject {
  [JsonPropertyName("@type")] public string Type { get; set; } = null!;

  [JsonPropertyName("name")] public string Name { get; set; } = null!;
}

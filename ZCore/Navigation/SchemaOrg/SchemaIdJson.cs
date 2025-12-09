using System.Text.Json.Serialization;
using IZ.Core.Data;

namespace IZ.Core.Navigation.SchemaOrg;

public class SchemaIdJson : TransientObject {
  [JsonPropertyName("@id")] public string Id { get; set; } = null!;
}

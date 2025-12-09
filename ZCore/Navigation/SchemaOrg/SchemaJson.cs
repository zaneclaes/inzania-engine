using System.Collections.Generic;
using System.Text.Json.Serialization;
using IZ.Core.Data;
using IZ.Core.Data.Attributes;

namespace IZ.Core.Navigation.SchemaOrg;

public class SchemaJson : TransientObject {
  [JsonPropertyName("@context")] public string SchemaContext { get; set; } = "https://schema.org";

  [JsonPropertyName("@graph")] [ApiFormat] public List<SchemaItemJson> Graph { get; set; } = new List<SchemaItemJson>();
}

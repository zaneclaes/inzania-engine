using System.Text.Json.Serialization;
using IZ.Core.Data;
using IZ.Core.Data.Attributes;

namespace IZ.Core.Navigation.SchemaOrg;

public class SchemaActionJson : TransientObject {
  [JsonPropertyName("@type")] public string Type { get; set; } = null!;

  [JsonPropertyName("name")] public string Name { get; set; } = null!;

  [JsonPropertyName("target")] [ApiFormat] public SchemaActionTargetJson? Target { get; set; }

  [JsonPropertyName("actionAccessibilityRequirement")] [ApiFormat] public SchemaRequirementJson? ActionAccessibilityRequirement { get; set; }

  [JsonPropertyName("expectsAcceptanceOf")] [ApiFormat] public SchemaIdJson? ExpectsAcceptanceOf { get; set; }
}

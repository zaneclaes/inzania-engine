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

  /// <summary>
  /// `query-input` — required by a `SearchAction`, and the reason a site gets the search box in its
  /// result. Its value is the literal `required name=<param>` micro-syntax, whose `<param>` has to
  /// match the placeholder in <see cref="Target" />'s url template.
  /// </summary>
  [JsonPropertyName("query-input")] public string? QueryInput { get; set; }
}

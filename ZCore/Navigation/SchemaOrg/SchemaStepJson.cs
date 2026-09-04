using System.Text.Json.Serialization;
using IZ.Core.Data;

namespace IZ.Core.Navigation.SchemaOrg;

/// <summary>
/// One `HowToStep`. A `HowTo` without ordered steps is not eligible for the rich result at all, so
/// this is the part that has to be right rather than the prose around it.
/// </summary>
public class SchemaStepJson : TransientObject {
  [JsonPropertyName("@type")] public string Type { get; set; } = "HowToStep";

  [JsonPropertyName("position")] public int Position { get; set; }

  [JsonPropertyName("name")] public string Name { get; set; } = null!;

  [JsonPropertyName("text")] public string Text { get; set; } = null!;

  /// <summary>A link to the step — for us, the section anchor it was read from.</summary>
  [JsonPropertyName("url")] public string? Url { get; set; }
}

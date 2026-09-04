using System.Text.Json.Serialization;
using IZ.Core.Data;

namespace IZ.Core.Navigation.SchemaOrg;

/// <summary>
/// One entry of a `BreadcrumbList` — the trail a search result shows above its title instead of a
/// bare URL. Position is 1-based, and the list has to be complete and in order or the whole
/// breadcrumb is dropped.
/// </summary>
public class SchemaListItemJson : TransientObject {
  [JsonPropertyName("@type")] public string Type { get; set; } = "ListItem";

  [JsonPropertyName("position")] public int Position { get; set; }

  [JsonPropertyName("name")] public string Name { get; set; } = null!;

  /// <summary>The page this crumb points at. Omitted on the last crumb, which is the current page —
  /// schema.org's own recommendation, since a breadcrumb that links to itself is noise.</summary>
  [JsonPropertyName("item")] public string? Item { get; set; }
}

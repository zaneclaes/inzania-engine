using System.Collections.Generic;
using System.Text.Json.Serialization;
using IZ.Core.Data;

namespace IZ.Core.Navigation.SchemaOrg;

public class SchemaActionTargetJson : TransientObject {
  [JsonPropertyName("@type")] public string Type { get; set; } = null!;

  [JsonPropertyName("urlTemplate")] public string UrlTemplate { get; set; } = null!;

  [JsonPropertyName("actionPlatform")] public List<string> ActionPlatform { get; set; } =
    new List<string>() {"https://schema.org/DesktopWebPlatform", "https://schema.org/MobileWebPlatform"};
}

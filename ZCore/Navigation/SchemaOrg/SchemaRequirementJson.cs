using System.Text.Json.Serialization;
using IZ.Core.Contexts;
using IZ.Core.Data;
using IZ.Core.Data.Attributes;

namespace IZ.Core.Navigation.SchemaOrg;

public class SchemaRequirementJson : TransientObject {
  [JsonPropertyName("@type")] public string Type { get; set; } = null!;

  [JsonPropertyName("requiresSubscription")] [ApiFormat] public SchemaEntityJson? RequiresSubscription { get; set; }

  public static SchemaRequirementJson ForSubscription(IZContext context, string name) => new SchemaRequirementJson() {
    Context = context,
    Type = "ActionAccessSpecification",
    RequiresSubscription = new SchemaEntityJson() {
      Context = context,
      Type = "MediaSubscription",
      Name = name,
    }
  };
}

using System.Text.Json.Serialization;
using IZ.Core.Data;
using IZ.Core.Data.Attributes;

namespace IZ.Core.Navigation.SchemaOrg;

public class SchemaOfferJson : TransientObject {
  [JsonPropertyName("@type")] public string Type { get; set; } = null!;

  [JsonPropertyName("@id")] public string Id { get; set; } = null!;

  [JsonPropertyName("name")] public string Name { get; set; } = null!;

  [JsonPropertyName("url")] public string? Url { get; set; }

  [JsonPropertyName("price")] public string Price { get; set; } = "0.00";

  [JsonPropertyName("priceCurrency")] public string PriceCurrency { get; set; } = "USD";

  [JsonPropertyName("availability")] public string Availability { get; set; } = "https://schema.org/InStock";

  [JsonPropertyName("eligibleRegion")] [ApiFormat] public SchemaEntityJson? EligibleRegion { get; set; }

  [JsonPropertyName("itemOffered")] [ApiFormat] public SchemaOfferItemJson? ItemOffered { get; set; }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using IZ.Core.Data;
using IZ.Core.Data.Attributes;

namespace IZ.Core.Navigation.SchemaOrg;

public class SchemaItemJson : TransientObject {
  [JsonPropertyName("@type")] public string Type { get; set; } = null!;

  [JsonPropertyName("@id")] public string Id { get; set; } = null!;

  [JsonPropertyName("url")] public string Url { get; set; } = null!;

  [JsonPropertyName("name")] public string Name { get; set; } = null!;

  [JsonPropertyName("image")] public string? Image { get; set; }
  [JsonPropertyName("thumbnailUrl")] public string? ThumbnailUrl { get; set; }

  [JsonPropertyName("description")] public string? Description { get; set; }

  [JsonPropertyName("inLanguage")] public string InLanguage { get; set; } = "en";

  [JsonPropertyName("isAccessibleForFree")] public bool IsAccessibleForFree { get; set; } = true;

  [JsonPropertyName("genres")] public List<string>? Genres { get; set; }

  [JsonPropertyName("mainEntity")] [ApiFormat] public SchemaIdJson? MainEntity { get; set; }
  [JsonPropertyName("isPartOf")] [ApiFormat] public SchemaWebSiteJson? IsPartOf { get; set; }

  // MusicComposition
  [JsonPropertyName("composer")] [ApiFormat] public SchemaEntityJson? Composer { get; set; }
  [JsonPropertyName("lyricist")] [ApiFormat] public SchemaEntityJson? Lyricist { get; set; }

  // MusicRecording
  [JsonPropertyName("byArtist")] [ApiFormat] public SchemaEntityJson? ByArtist { get; set; }

  // Offers & Actions
  [JsonPropertyName("offers")] [ApiFormat] public List<SchemaOfferJson> Offers { get; set; } = new List<SchemaOfferJson>();
  [JsonPropertyName("potentialAction")] [ApiFormat] public List<SchemaActionJson> PotentialAction { get; set; } = new List<SchemaActionJson>();

  public SchemaItemJson WithGenres(params string[] genres) {
    Genres ??= new List<string>();
    Genres.AddRange(genres);
    return this;
  }

  public SchemaItemJson AsPartOf(string url, string name) {
    IsPartOf = new SchemaWebSiteJson() {
      Context = Context,
      Id = url,
      Name = name,
      Url = url,
    };
    return this;
  }

  public SchemaItemJson WithMainEntityId(string id) {
    MainEntity = new SchemaIdJson() {
      Context = Context,
      Id = id,
    };
    return this;
  }

  public SchemaItemJson WithComposer(string name) {
    Composer = new SchemaEntityJson() {
      Context = Context,
      Type = "Person",
      Name = name,
    };
    return this;
  }

  public SchemaItemJson WithLyricist(string name) {
    Lyricist = new SchemaEntityJson() {
      Context = Context,
      Type = "Person",
      Name = name,
    };
    return this;
  }

  public SchemaItemJson WithArtist(string name) {
    ByArtist = new SchemaEntityJson() {
      Context = Context,
      Type = "MusicGroup",
      Name = name,
    };
    return this;
  }

  public SchemaItemJson WithOffer(string actionType, string name, string url, params string[] platforms) {
    var offerId = url;
    Offers.Add(new SchemaOfferJson() {
      Context = Context,
      Type = "Offer",
      Id = offerId,
      Name = name,
    });

    PotentialAction.Add(new SchemaActionJson() {
      Context = Context,
      Type = actionType,
      Name = name,
      Target = new SchemaActionTargetJson() {
        Type = "EntryPoint",
        UrlTemplate = url,
        ActionPlatform = platforms.ToList(),
      },
      ExpectsAcceptanceOf = new SchemaIdJson() {
        Id = offerId,
      }
    });

    return this;
  }

  public SchemaItemJson WithDownload(string name, string url, string? subscriptionName = null) {
    var offerId = url;
    Offers.Add(new SchemaOfferJson() {
      Context = Context,
      Type = "Offer",
      Id = offerId,
      Name = name,
    });

    var req = subscriptionName == null ? null : SchemaRequirementJson.ForSubscription(Context, name);
    PotentialAction.Add(new SchemaActionJson() {
      Context = Context,
      Type = "ViewAction",
      Name = name,
      Target = new SchemaActionTargetJson() {
        Type = "EntryPoint",
        UrlTemplate = url,
      },
      ActionAccessibilityRequirement = req,
      ExpectsAcceptanceOf = new SchemaIdJson() {
        Id = offerId,
      }
    });

    return this;
  }
}

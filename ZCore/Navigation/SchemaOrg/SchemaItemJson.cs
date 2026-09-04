using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using IZ.Core.Data;
using IZ.Core.Data.Attributes;
using IZ.Core.Json;

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

  [JsonIgnore] public SchemaIdJson? MainEntity { get; set; }

  /// <summary>The `Question` list of an `FAQPage`.</summary>
  [JsonIgnore] public List<SchemaQuestionJson>? Questions { get; set; }

  /// <summary>
  /// `mainEntity`, which schema.org lets be either a reference or a list: a `WebPage` points at the
  /// one thing it is about, an `FAQPage` carries its questions inline. Two properties cannot share a
  /// JSON name, so the union is expressed here — the list when there is one, the reference otherwise.
  /// </summary>
  /// <remarks>
  /// Hidden from the API surface. `object` has no fields, and `ZApiTypeGenerator` scanning it emits
  /// a `ZObjectType&lt;System.Object&gt;` that HotChocolate refuses to build — "the object type
  /// `Object` has to at least define one field". These items only ever cross the wire as serialized
  /// JSON inside a string column, so nothing is lost by excluding them.
  /// </remarks>
  [JsonPropertyName("mainEntity")] [OutputIgnore] [InputIgnore]
  public object? MainEntityValue {
    get => Questions != null && Questions.Count > 0 ? Questions : (object?) MainEntity;
    set {
      Questions = null;
      MainEntity = null;
      switch (value) {
        case List<SchemaQuestionJson> questions:
          Questions = questions;
          return;
        case SchemaIdJson id:
          MainEntity = id;
          return;
        case JsonElement element when element.ValueKind == JsonValueKind.Array:
          Questions = ZJson.DeserializeObject<List<SchemaQuestionJson>>(Context, element.GetRawText());
          return;
        case JsonElement element when element.ValueKind == JsonValueKind.Object:
          MainEntity = ZJson.DeserializeObject<SchemaIdJson>(Context, element.GetRawText());
          return;
      }
    }
  }

  [JsonPropertyName("isPartOf")] [ApiFormat] public SchemaWebSiteJson? IsPartOf { get; set; }

  // --- Article and anything else with a date and a byline ---

  [JsonPropertyName("headline")] public string? Headline { get; set; }

  /// <summary>ISO 8601. A date Google cannot parse is worse than no date.</summary>
  [JsonPropertyName("datePublished")] public string? DatePublished { get; set; }

  [JsonPropertyName("dateModified")] public string? DateModified { get; set; }

  [JsonPropertyName("author")] [ApiFormat] public SchemaEntityJson? Author { get; set; }

  [JsonPropertyName("publisher")] [ApiFormat] public SchemaEntityJson? Publisher { get; set; }

  /// <summary>`BreadcrumbList` entries, 1-based and in order.</summary>
  [JsonPropertyName("itemListElement")] [ApiFormat]
  public List<SchemaListItemJson>? ItemListElement { get; set; }

  /// <summary>`HowTo` steps. Without them a `HowTo` is not eligible for the rich result.</summary>
  [JsonPropertyName("step")] [ApiFormat] public List<SchemaStepJson>? Steps { get; set; }

  /// <summary>ISO 8601 duration, e.g. `PT15M`.</summary>
  [JsonPropertyName("totalTime")] public string? TotalTime { get; set; }

  // --- VideoObject ---

  [JsonPropertyName("embedUrl")] public string? EmbedUrl { get; set; }

  [JsonPropertyName("contentUrl")] public string? ContentUrl { get; set; }

  [JsonPropertyName("uploadDate")] public string? UploadDate { get; set; }

  // --- Course and LearningResource ---

  [JsonPropertyName("provider")] [ApiFormat] public SchemaEntityJson? Provider { get; set; }

  [JsonPropertyName("learningResourceType")] public string? LearningResourceType { get; set; }

  [JsonPropertyName("educationalLevel")] public string? EducationalLevel { get; set; }

  [JsonPropertyName("teaches")] public List<string>? Teaches { get; set; }

  [JsonPropertyName("hasPart")] [ApiFormat] public List<SchemaIdJson>? HasPart { get; set; }

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
      Type = "DownloadAction",
      Name = name,
      Target = new SchemaActionTargetJson() {
        Type = "EntryPoint",
        UrlTemplate = url,
        ActionPlatform = new List<string>() {
          "https://schema.org/DesktopWebPlatform",
          "https://schema.org/MobileWebPlatform"
        }
      },
      ActionAccessibilityRequirement = req,
      ExpectsAcceptanceOf = new SchemaIdJson() {
        Id = offerId,
      }
    });

    return this;
  }
}

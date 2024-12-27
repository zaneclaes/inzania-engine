#region

using System.Text.Json.Serialization;
using IZ.Core.Observability.Analytics;

#endregion

namespace IZ.Client.GoogleAnalytics.Events;

public class ContentEventParams : IEventParams {
  [JsonPropertyName("content_type")] public string ContentType { get; set; } = default!;

  [JsonPropertyName("content_id")] public string ContentId { get; set; } = default!;
}

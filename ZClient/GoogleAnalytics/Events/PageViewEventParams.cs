#region

using System.Text.Json.Serialization;
using IZ.Core.Observability.Analytics;

#endregion

namespace IZ.Client.GoogleAnalytics.Events;

public class PageViewEventParams : IEventParams {
  [JsonPropertyName("page_location")] public string Path { get; set; } = default!;

  [JsonPropertyName("page_title")] public string? Title { get; set; }
}

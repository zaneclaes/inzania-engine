#region

using System.Text.Json.Serialization;
using IZ.Core.Observability.Analytics;

#endregion

namespace IZ.Client.GoogleAnalytics.Events;

public class ScreenViewEventParams : IEventParams {
  [JsonPropertyName("screen_class")] public string? Class { get; set; }

  [JsonPropertyName("screen_name")] public string Name { get; set; } = default!;
}

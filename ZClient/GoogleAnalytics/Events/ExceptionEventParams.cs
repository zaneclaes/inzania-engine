#region

using System.Text.Json.Serialization;
using IZ.Core.Observability.Analytics;

#endregion

namespace IZ.Client.GoogleAnalytics.Events;

public class ExceptionEventParams : IEventParams {
  [JsonPropertyName("description")] public string Description { get; set; } = default!;

  [JsonPropertyName("fatal")] public bool IsFatal { get; set; } = false;
}

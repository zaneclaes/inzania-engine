#region

using System.Text.Json.Serialization;
using IZ.Core.Observability.Analytics;

#endregion

namespace IZ.Client.GoogleAnalytics.Events;

public class MethodEventParams : IEventParams {
  [JsonPropertyName("method")] public string Method { get; set; } = default!;
}

#region

using System.Text.Json.Serialization;
using IZ.Core.Observability.Analytics;

#endregion

namespace IZ.Client.GoogleAnalytics.Events;

public class MethodEventParams : BaseParams {
  [JsonPropertyName("method")] public string Method { get; set; } = null!;
  [JsonPropertyName("item_id")] public string ItemId { get; set; } = null!;
}

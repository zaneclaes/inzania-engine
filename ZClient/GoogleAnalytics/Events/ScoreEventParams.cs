#region

using System.Text.Json.Serialization;
using IZ.Core.Observability.Analytics;

#endregion

namespace IZ.Client.GoogleAnalytics.Events;

public class ScoreEventParams : BaseParams {
  [JsonPropertyName("score")] public long Score { get; set; }

  [JsonPropertyName("level")] public int? Level { get; set; }

  [JsonPropertyName("character")] public string? Character { get; set; }
}

using System.Text.Json.Serialization;
using IZ.Core.Observability.Analytics;

namespace IZ.Client.GoogleAnalytics.Events;

public class UserEngagementEventParams : BaseParams {
  [JsonPropertyName("engagement_time_msec")] public long EngagementTimeMsec { get; set; }
}

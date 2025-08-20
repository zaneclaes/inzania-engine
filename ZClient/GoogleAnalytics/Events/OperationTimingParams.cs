using System.Text.Json.Serialization;
using IZ.Core.Observability.Analytics;

namespace IZ.Client.GoogleAnalytics.Events;

public class OperationTimingParams : BaseParams {
  [JsonPropertyName("operation_name")] public string OperationName { get; set; } = null!;

  [JsonPropertyName("duration_ms")] public long DurationMs { get; set; }

  [JsonPropertyName("success")] public int Success { get; set; }

  [JsonPropertyName("error_code")] public string? ErrorCode { get; set; } = null!;
}

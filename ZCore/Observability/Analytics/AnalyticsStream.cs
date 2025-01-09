#region

using System.Text.Json.Serialization;
using IZ.Core.Contexts;
using IZ.Core.Data.Attributes;

#endregion

namespace IZ.Core.Observability.Analytics;

public class AnalyticsStream : IAmInternal {
  public string Name { get; }
  public string MeasurementId { get; }
  public long StreamId { get; }

  [ApiIgnore] [JsonIgnore]
  public string ApiSecret { get; } = "nMlXmBNYQfqcFu9oSvZ-eg";

  public AnalyticsStream(string name, string mId, long streamId, string? apiSecret = null) {
    Name = name;
    MeasurementId = mId;
    StreamId = streamId;
    if (apiSecret != null) ApiSecret = apiSecret;
  }
}

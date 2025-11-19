#region

using System.Text.Json.Serialization;
using IZ.Core.Contexts;
using IZ.Core.Data;
using IZ.Core.Data.Attributes;

#endregion

namespace IZ.Core.Observability.Analytics;

public class AnalyticsOptions : AppOptions<AnalyticsOptions> {
  [Observable] public string Name { get; set; } = null!;
  [Observable] public string MeasurementId { get; set; } = null!;
  [Observable] public long StreamId { get; set; }
  public bool Debug { get; set; }
  [ApiSecret] public string ApiSecret { get; set; } = null!; // NOT kept secret for now... may need to change to server intermediary model

  public AnalyticsOptions() : base() {}

  // public AnalyticsOptions(string name, string mId, long streamId, string? apiSecret = null) {
  //   Name = name;
  //   MeasurementId = mId;
  //   StreamId = streamId;
  //   if (apiSecret != null) ApiSecret = apiSecret;
  // }
}

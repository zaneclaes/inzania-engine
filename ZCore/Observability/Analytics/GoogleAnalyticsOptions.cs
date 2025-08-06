using IZ.Core.Data;
using IZ.Core.Data.Attributes;

namespace IZ.Core.Observability.Analytics;

public class GoogleAnalyticsOptions : TransientObject {
  [Observable] public string MeasurementId { get; set; } = null!;
  [Observable] public long StreamId { get; set; }
}

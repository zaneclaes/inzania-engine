#region

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

#endregion

namespace IZ.Core.Observability.Analytics;

public interface IAnalyticsSink : IDisposable {
  public Task<bool> SendEvent(AnalyticsEvent e); //  where T : IEventParams;

  public Task Config(AnalyticsStream stream, string installId, string sessionId, string? userId = null, Dictionary<string, object>? userProps = null);
}

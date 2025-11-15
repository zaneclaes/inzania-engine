using System;
using System.Collections.Generic;
using IZ.Core.Auth;
using IZ.Core.Utils;
#region

#endregion

namespace IZ.Core.Observability.Analytics;

public interface IAnalyticsSink : IDisposable {
  public ZTask SendEvent(AnalyticsEvent e); //  where T : IEventParams;

  public ZTask Config(AnalyticsOptions options, Installation install, IZIdentity? identity = null, Dictionary<string, object>? userProps = null);

  public ZTask SetIdentity(IZIdentity? identity = null, Dictionary<string, object>? userProps = null);
}

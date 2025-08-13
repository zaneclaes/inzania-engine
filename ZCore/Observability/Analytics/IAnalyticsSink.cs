using System;
using System.Collections.Generic;
using IZ.Core.Auth;
#region

#if Z_UNITY
using Cysharp.Threading.Tasks;
using ZTask = Cysharp.Threading.Tasks.UniTask;
using Tasks = Cysharp.Threading.Tasks.UniTask;
#else
using ZTask = System.Threading.Tasks.Task;
#endif

#endregion

namespace IZ.Core.Observability.Analytics;

public interface IAnalyticsSink : IDisposable {
  public ZTask SendEvent(AnalyticsEvent e); //  where T : IEventParams;

  public ZTask Config(AnalyticsOptions options, Installation install, IZIdentity? identity = null, Dictionary<string, object>? userProps = null);

  public ZTask SetIdentity(IZIdentity? identity = null, Dictionary<string, object>? userProps = null);
}

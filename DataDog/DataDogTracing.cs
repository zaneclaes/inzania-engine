#region

using Datadog.Trace;
using Datadog.Trace.Configuration;
using IZ.Core;
using IZ.Core.Contexts;

#endregion

namespace IZ.Observability.DataDog;

public static class DataDogTracing {
  public static void Enable() {
    var tracerSettings = TracerSettings.FromDefaultSources();
    tracerSettings.ServiceName = IZEnv.ProductName;
    // tracerSettings.GlobalTags.Add("dd_env", FurEnv.AspNetEnv.Equals(FurEnv.ProductionEnv) ? "prod" : FurEnv.AspNetEnv.ToLowerInvariant());
    // tracerSettings.GlobalTags.Add("dd_version", Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0.0");
    tracerSettings.GlobalTags.Add("service", IZEnv.ProductName);
    // tracerSettings.GlobalTags.Add("subdomain", TuneEnv.Subdomain);
    // tracerSettings.GlobalTags.Add("pod_role", TuneEnv.IsTimekeeperDaemon ? "worker" : "web");
    tracerSettings.LogsInjectionEnabled = true;
    Tracer.Configure(tracerSettings);

    IZEnv.SpanBuilder = BuildSpan;
  }

  private static ITuneSpan BuildSpan(ITuneContext context) => new DataDogSpan(context);
}

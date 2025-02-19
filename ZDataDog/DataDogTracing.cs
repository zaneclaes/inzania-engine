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
    tracerSettings.ServiceName = ZEnv.ProductName;
    // tracerSettings.GlobalTags.Add("dd_env", FurEnv.AspNetEnv.Equals(FurEnv.ProductionEnv) ? "prod" : FurEnv.AspNetEnv.ToLowerInvariant());
    // tracerSettings.GlobalTags.Add("dd_version", Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0.0");
    tracerSettings.GlobalTags.Add("service", ZEnv.ProductName);
    tracerSettings.LogsInjectionEnabled = true;
    Tracer.Configure(tracerSettings);

    ZEnv.SpanBuilder = BuildSpan;
  }

  private static IZSpan BuildSpan(IZContext context) => new DataDogSpan(context);
}

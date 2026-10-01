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
    tracerSettings.GlobalTags.Add("service", ZEnv.ProductName);
    foreach (var tag in RuntimeWorkload.Capture()) {
      if (tag.Key == "version") tracerSettings.ServiceVersion = tag.Value;
      else tracerSettings.GlobalTags[tag.Key] = tag.Value;
    }
    tracerSettings.LogsInjectionEnabled = true;
    Tracer.Configure(tracerSettings);

    ZEnv.SpanBuilder = BuildSpan;
    ZEnv.OnCaughtException = (ex, source) =>
      RuntimeExceptionSpan.RecordOnActive(Tracer.Instance.ActiveScope?.Span, ex, source);
  }

  private static IZSpan BuildSpan() => new DataDogSpan();
}

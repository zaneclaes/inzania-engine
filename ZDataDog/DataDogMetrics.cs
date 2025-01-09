#region

using System;
using System.Collections.Generic;
using System.Linq;
using IZ.Core;
using IZ.Core.Contexts;
using IZ.Core.Observability.Metrics;
using StatsdClient;

#endregion

namespace IZ.Observability.DataDog;

public class DataDogMetrics : LogicBase, IZMetrics {
  private DogStatsdService? _metrics;
  private DogStatsdService Metrics {
    get {
      if (_metrics != null) return _metrics;
      _metrics = new DogStatsdService();
      string? subdomain = Context.App.SubDomain;
      List<string> ddTags = new List<string> {
        $"env:{Context.App.Env.ToShortString()}"
      };
      if (subdomain != null) ddTags.Add($"subdomain:{subdomain}");
      // var props = Context.GetMetricTags();
      // foreach (string key in props.Keys) ddTags.Add($"{key.Replace(".", "_")}:{props[key]}");
      var cfg = new StatsdConfig {
        ConstantTags = ddTags.ToArray()
      };
      if (Context.App.Env <= ZEnvironment.Development) cfg.StatsdServerName = "10.0.0.111";
      _metrics.Configure(cfg);
      return _metrics;
    }
  }

  public DataDogMetrics(IZContext context) : base(context) { }

  private const double SampleRate = 1.0;

  private static string[] FormatTags(Dictionary<string, object>? tags) => tags == null ? new string[] { } :
    tags.Select(t => $"{t.Key}:{t.Value}").ToArray();

  public void PageView(string path, string? title = null, Dictionary<string, object>? tags = null) { }

  public void Timer(string metric, TimeSpan elapsed, Dictionary<string, object>? tags = null) =>
    Metrics.Timer(metric, elapsed.TotalMilliseconds, SampleRate, FormatTags(tags));

  public void Gauge(string metric, double value, Dictionary<string, object>? tags = null) =>
    Metrics.Gauge(metric, value, SampleRate, FormatTags(tags));

  public void Counter(string metric, double value, Dictionary<string, object>? tags = null) =>
    Metrics.Counter(metric, value, SampleRate, FormatTags(tags));

  public void Increment(string metric, int value = 1, Dictionary<string, object>? tags = null) =>
    Metrics.Increment(metric, value, SampleRate, FormatTags(tags));

  public void Decrement(string metric, int value = 1, Dictionary<string, object>? tags = null) =>
    Metrics.Decrement(metric, value, SampleRate, FormatTags(tags));

  public void Histogram(string metric, double value, Dictionary<string, object>? tags = null) =>
    Metrics.Histogram(metric, value, SampleRate, FormatTags(tags));

  public void Event(
    string title,
    string text,
    string? alertType = null,
    string? aggregationKey = null,
    string? sourceType = null,
    int? dateHappened = null,
    string? priority = null,
    string? hostname = null,
    Dictionary<string, object>? tags = null
  ) => Metrics.Event(title, text, alertType, aggregationKey, sourceType, dateHappened, priority, hostname, FormatTags(tags));

  public override void Dispose() {
    base.Dispose();
    _metrics?.Dispose();
    _metrics = null;
  }
}

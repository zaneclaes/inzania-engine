#region

using System;
using System.Collections.Generic;
using System.Linq;
using IZ.Core.Observability.Logging;

#endregion

namespace IZ.Core.Observability;

public static class ZMetrics {
  public static readonly string Root = ZEnv.ProductName.ToLower();

  public static readonly string VerbCreate = "create";
  public static readonly string VerbUpdate = "update";
  public static readonly string VerbDelete = "delete";
  public static readonly string VerbOpen = "open";

  public static readonly string UsersGroup = $"{Root}.users";
  public static readonly string UsersAuthGroup = $"{UsersGroup}.auth";

  public static readonly string SysGroup = $"{Root}.sys";

  // Time spent playing
  public static readonly string PlayGroup = $"{Root}.play";
  public static readonly string PlayCountGroup = $"{PlayGroup}.count";
  public static readonly string PlayTimeGroup = $"{PlayGroup}.time";
  public static readonly string PlaySessionGroup = $"{PlayGroup}.session";
  public static readonly string PlayScoreGroup = $"{PlayGroup}.score";
  public static readonly string PlayPointsGroup = $"{PlayGroup}.points";

  public static readonly string GradeGroup = $"{Root}.grade";
  public static readonly string GradeNotesGroup = $"{GradeGroup}.notes";
  public static readonly string GradeScoresGroup = $"{GradeGroup}.scores";

  public static readonly string SkillGroup = $"{Root}.skill";
  public static readonly string SkillPointsGroup = $"{SkillGroup}.points";
  public static readonly string SkillScoreGroup = $"{SkillGroup}.score";
  public static readonly string SkillLevelGroup = $"{SkillGroup}.level";

  private static Dictionary<string, object> GetTags(this IEventEnricher m, Dictionary<string, object>? tags = null) {
    Dictionary<string, object>? ret = m.EventTags.ToDictionary(k => k.Key, k => k.Value);
    if (tags != null)
      foreach (string? k in tags.Keys)
        ret[k] = tags[k];
    return ret;
  }

  public static void TimerMetric(this IEventEnricher m, string metric, TimeSpan elapsed, Dictionary<string, object>? tags = null) =>
    m.Context.Metrics?.Timer(metric, elapsed, m.GetTags(tags));

  public static void GaugeMetric(this IEventEnricher m, string metric, double value, Dictionary<string, object>? tags = null) =>
    m.Context.Metrics?.Gauge(metric, value, m.GetTags(tags));

  public static void CounterMetric(this IEventEnricher m, string metric, double value, Dictionary<string, object>? tags = null) =>
    m.Context.Metrics?.Counter(metric, value, m.GetTags(tags));

  public static void IncrementMetric(this IEventEnricher m, string metric, int value = 1, Dictionary<string, object>? tags = null) =>
    m.Context.Metrics?.Increment(metric, value, m.GetTags(tags));

  public static void DecrementMetric(this IEventEnricher m, string metric, int value = 1, Dictionary<string, object>? tags = null) =>
    m.Context.Metrics?.Decrement(metric, value, m.GetTags(tags));

  public static void HistogramMetric(this IEventEnricher m, string metric, double value, Dictionary<string, object>? tags = null) =>
    m.Context.Metrics?.Histogram(metric, value, m.GetTags(tags));
}

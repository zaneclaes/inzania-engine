#region

using System;
using System.Collections.Generic;
using IZ.Core.Contexts;

#endregion

namespace IZ.Core.Observability.Metrics;

public interface ITuneMetrics : IAmInternal {
  public void Timer(string metric, TimeSpan elapsed, Dictionary<string, object>? tags = null);

  public void Gauge(string metric, double value, Dictionary<string, object>? tags = null);

  public void Counter(string metric, double value, Dictionary<string, object>? tags = null);

  public void Increment(string metric, int value = 1, Dictionary<string, object>? tags = null);

  public void Decrement(string metric, int value = 1, Dictionary<string, object>? tags = null);

  public void Histogram(string metric, double value, Dictionary<string, object>? tags = null);

  /// <summary>Records an event.</summary>
  /// <param name="title">The title of the event.</param>
  /// <param name="text">The text body of the event.</param>
  /// <param name="alertType">error, warning, success, or info (defaults to info).</param>
  /// <param name="aggregationKey">A key to use for aggregating events.</param>
  /// <param name="sourceType">The source type name.</param>
  /// <param name="dateHappened">The epoch timestamp for the event (defaults to the current time from the DogStatsD server).</param>
  /// <param name="priority">Specifies the priority of the event (normal or low).</param>
  /// <param name="hostname">The name of the host.</param>
  /// <param name="tags">Array of tags to be added to the data.</param>
  public void Event(
    string title,
    string text,
    string? alertType = null,
    string? aggregationKey = null,
    string? sourceType = null,
    int? dateHappened = null,
    string? priority = null,
    string? hostname = null,
    Dictionary<string, object>? tags = null);
}

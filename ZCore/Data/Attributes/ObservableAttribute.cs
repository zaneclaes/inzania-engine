#region

using System;

#endregion

namespace IZ.Core.Data.Attributes;

/// <summary>
///   Properties with this flag are not available via the Furballs API.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class ObservableAttribute : Attribute {
  public string? MetricName { get; }

  public ObservableAttribute(string? metricName = null) {
    MetricName = metricName;
  }
}

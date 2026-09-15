namespace IZ.Core.Observability.Analytics;

/// <summary>Transport permission, not an event tag. Unknown is intentionally fail-closed.</summary>
public enum AnalyticsTrafficStatus { Unknown, External, Internal }

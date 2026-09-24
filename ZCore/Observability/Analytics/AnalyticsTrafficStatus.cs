namespace IZ.Core.Observability.Analytics;

/// <summary>
/// Transport permission, not an event tag. Only <see cref="Internal" /> excludes traffic.
/// <see cref="Unknown" /> means no verdict has arrived yet: events wait in a bounded queue, are sent
/// when the verdict is <see cref="External" />, and are dropped when it is <see cref="Internal" />.
/// A verdict that cannot tell is <see cref="External" />; unknown traffic is counted.
/// </summary>
public enum AnalyticsTrafficStatus { Unknown, External, Internal }

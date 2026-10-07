using System;
using System.Collections.Generic;
using IZ.Core.Auth;
using IZ.Core.Utils;
#region

#endregion

namespace IZ.Core.Observability.Analytics;

public interface IAnalyticsSink : IDisposable {
  public ZTask SendEvent(AnalyticsEvent e); //  where T : IEventParams;

  public ZTask Config(AnalyticsOptions options, Installation install, IZIdentity? identity = null, Dictionary<string, object>? userProps = null);

  public ZTask SetIdentity(IZIdentity? identity = null, Dictionary<string, object>? userProps = null);

  /// <summary>Stops transport immediately when an install becomes internal or unknown.</summary>
  public ZTask SetTrafficStatus(AnalyticsTrafficStatus status);
}

/// <summary>A first-party capture owner applies purpose permission and exclusions itself; it has no GA transport.</summary>
public interface IAnalyticsCaptureSink : IAnalyticsSink {
  ZTask Capture(AnalyticsCapture capture);
}

/// <summary>Values at emission, copied before a later verdict, identity or mutable params can change them.</summary>
public sealed class AnalyticsCapture {
  public AnalyticsCapture(string name, string? parameters, long sessionId, DateTime at,
    AnalyticsTrafficStatus traffic, bool optionalCollectionAllowed) {
    Name = name; Parameters = parameters; SessionId = sessionId; At = at;
    Traffic = traffic; OptionalCollectionAllowed = optionalCollectionAllowed;
  }
  public string Name { get; }
  public string? Parameters { get; }
  public long SessionId { get; }
  public DateTime At { get; }
  public AnalyticsTrafficStatus Traffic { get; }
  public bool OptionalCollectionAllowed { get; }
}

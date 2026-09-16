#region

using System;
using System.Linq;
using System.Text.Json.Serialization;
using IZ.Core.Auth;
using IZ.Core.Utils;

#endregion

namespace IZ.Core.Observability.Analytics;

public interface IEventParams {
  public long SessionId { get; set; }
  public long SessionNumber { get; set; }

  public void LoadInstallation(Installation installation);
}

public class AnalyticsEvent {

  public AnalyticsEvent(string name, IEventParams? pars = null) {
    Name = name;
    EventParams = pars;
  }
  [JsonPropertyName("name")] public string Name { get; set; }

  [JsonIgnore] public IEventParams? EventParams { get; set; }
}

// https://developers.google.com/analytics/devguides/collection/protocol/ga4/reference?client_type=gtag#common_params
public class BaseParams : IEventParams {
  [JsonPropertyName("traffic_type")] public string? TrafficType { get; set; }
  [JsonPropertyName("ga_session_id")] public long SessionId { get; set; }
  [JsonPropertyName("ga_session_number")] public long SessionNumber { get; set; }
  [JsonPropertyName("engagement_time_msec")] public long? EngagementTimeMsec { get; set; }

  [JsonPropertyName("city")] public string? City { get; set; }
  [JsonPropertyName("region_id")] public string? RegionId { get; set; }
  [JsonPropertyName("country_id")] public string? CountryId { get; set; }
  [JsonPropertyName("subcontinent_id")] public string? SubcontinentId { get; set; }
  [JsonPropertyName("continent_id")] public string? ContinentId { get; set; }

  [JsonPropertyName("category")] public string? Category { get; set; } // desktop, tablet, mobile
  [JsonPropertyName("language")] public string? Language { get; set; } // en, en-US
  [JsonPropertyName("screen_resolution")] public string? ScreenResolution { get; set; } // WIDTHxHEIGHT
  [JsonPropertyName("operating_system")] public string? OperatingSystem { get; set; } // MacOS
  [JsonPropertyName("operating_system_version")] public string? OperatingSystemVersion { get; set; } // 13.5
  [JsonPropertyName("model")] public string? Model { get; set; } // Pixel 9, blah blah
  [JsonPropertyName("brand")] public string? Brand { get; set; } // Apple
  [JsonPropertyName("browser")] public string? Browser { get; set; }
  [JsonPropertyName("browser_version")] public string? BrowserVersion { get; set; }

  // GA4 data filters match on traffic_type: "internal" hides Dev/Staging from every report, "app"
  // separates the game client from the website so web acquisition reports stay clean.
  public const string TrafficTypeInternal = "internal";
  public const string TrafficTypeApp = "app";

  /// <summary>The query parameter a URL carries when the visit itself is the product's own traffic —
  /// a Lighthouse audit, a smoke test, a page opened from the owner's machine. The page tagger reads
  /// it before loading any vendor script, the GA4 reports filter it out, and the deployment verifiers
  /// recognise an audit by it.</summary>
  public const string TrafficQueryKey = "traffic";

  /// <summary>`traffic=internal`, the one spelling of the flag as it appears in a URL.</summary>
  public const string TrafficQueryFlag = TrafficQueryKey + "=" + TrafficTypeInternal;

  /// <summary>
  /// <paramref name="url" /> with the internal-traffic flag on it as a real query parameter: joined
  /// with `?` or `&amp;` depending on what is already there, and always *before* a `#fragment`, which
  /// is put back afterwards.
  ///
  /// Composing this by hand is what this exists to stop. `${host}/${path}?traffic=internal` turns a
  /// path that already has a query into `?x=1?traffic=internal` and a path with a fragment into
  /// `#play?traffic=internal` — neither of which any reader of the flag recognises, so the audit
  /// silently becomes ordinary traffic and the gate that watches it is skipped. Already-flagged URLs
  /// are returned unchanged.
  /// </summary>
  public static string WithInternalTrafficFlag(string? url) {
    string rest = url ?? string.Empty;
    string fragment = string.Empty;
    int hash = rest.IndexOf('#');
    if (hash >= 0) {
      fragment = rest.Substring(hash);
      rest = rest.Substring(0, hash);
    }
    if (IsInternalTrafficUrl(rest)) return rest + fragment;
    return rest + (rest.IndexOf('?') >= 0 ? '&' : '?') + TrafficQueryFlag + fragment;
  }

  /// <summary>
  /// Whether <paramref name="url" /> carries the flag as its own query parameter. Absolute URLs and
  /// the relative paths GA reports hand back are both read the same way: cut the fragment, take what
  /// follows the first `?`, and require one whole `&amp;`-separated part to be the flag — so
  /// `?trafficking=internal-hosts` and the mis-joined `?x=1?traffic=internal` are correctly not it.
  /// </summary>
  public static bool IsInternalTrafficUrl(string? url) {
    string query = QueryOf(url);
    if (query.Length <= 0) return false;
    foreach (string part in query.Split(new[] { '&' }, StringSplitOptions.RemoveEmptyEntries)) {
      if (string.Equals(part, TrafficQueryFlag, StringComparison.OrdinalIgnoreCase)) return true;
    }
    return false;
  }

  private static string QueryOf(string? url) {
    if (string.IsNullOrEmpty(url)) return string.Empty;
    string value = url!;
    int hash = value.IndexOf('#');
    if (hash >= 0) value = value.Substring(0, hash);
    int question = value.IndexOf('?');
    return question < 0 ? string.Empty : value.Substring(question + 1);
  }

  public void LoadInstallation(Installation installation) {
    // "internal" is sticky: an emitter that has already decided this process is the product's own
    // traffic (its owner's machine, a smoke test) must not have that decision downgraded to "app" or
    // to nothing by the per-event defaults below, which know only the environment and the device.
    if (TrafficType == TrafficTypeInternal) {
      // keep
    } else if (installation.Context.App.Env <= ZEnvironment.Staging) {
      TrafficType = TrafficTypeInternal;
    } else if (installation.DeviceType != DeviceType.Browser) {
      TrafficType = TrafficTypeApp;
    }
    Language = installation.Language;
    ScreenResolution = $"{installation.ScreenWidth}x{installation.ScreenHeight}";
    Model = installation.Model;
    OperatingSystem = installation.OsFamily;
    OperatingSystemVersion = installation.Os.Split(" ").Last();
    Category = installation.DeviceType.ToString().ToLowerInvariant();
  }
}

public class AnalyticsEvent<T> : AnalyticsEvent where T : IEventParams {

  public AnalyticsEvent(string name, T? pars) : base(name, pars) {
    Params = pars;
  }
  [JsonPropertyName("params")] public T? Params { get; set; }
}

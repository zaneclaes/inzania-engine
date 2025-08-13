#region

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

  public void LoadInstallation(Installation installation) {
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

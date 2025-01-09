#region

using System.Collections.Generic;
using System.Text.Json.Serialization;
using IZ.Core;
using IZ.Core.Utils;

#endregion

namespace IZ.Client.GoogleAnalytics;

public interface IUserParams { }

public class GaUserProp {
  [JsonIgnore] public string Name { get; set; } = default!;
  [JsonPropertyName("value")] public object Value { get; set; } = default!;
}

// https://www.thyngster.com/ga4-measurement-protocol-cheatsheet/
// https://developers.google.com/analytics/devguides/collection/protocol/ga4/reference?client_type=gtag#payload
public class GaParams {
  [JsonPropertyName("client_id")] public string ClientId { get; set; }

  [JsonPropertyName("user_id")] public string? UserId { get; set; }

  [JsonPropertyName("timestamp_micros")] public long Timestamp { get; set; }

  [JsonPropertyName("user_properties")] public Dictionary<string, object> UserProperties { get; set; } = new Dictionary<string, object>();

  [JsonPropertyName("events")] public List<object> Events { get; set; } = new List<object>();

  public GaParams(string clientId, string? userId = null, Dictionary<string, object>? userProps = null) {
    ClientId = clientId;
    UserId = userId;
    Timestamp = (long) (ZEnv.Now.GetUnixTimestampSec() * 1000000.0); // MICROseconds
    if (userProps != null) {
      foreach (string? k in userProps.Keys)
        UserProperties[k] = new GaUserProp {
          Name = k,
          Value = userProps[k]
        };
    }
  }

  // [JsonPropertyName("v")] public int ProtocolVersion { get; set; } = 2;
  //
  // [JsonPropertyName("tid")] public string TrackingId { get; set; }
  //
  // [JsonPropertyName("gtm")] public string GtmHash { get; set; }
  //
  // [JsonPropertyName("_p")] public string RandomHash { get; set; } = ModelId.GenerateId();
  //
  // [JsonPropertyName("gcd")] public string GoogleConsentDefault { get; set; } = "13l3l3l3l1";
  //
  // [JsonPropertyName("npa")] public int Npa { get; set; } = 0;
  //
  // [JsonPropertyName("dma")] public int Dma { get; set; } = 0;
  //
  // [JsonPropertyName("cid")] public string ClientId { get; set; }
  //
  // [JsonPropertyName("ul")] public string? Language { get; set; } // "en-us";
  //
  // [JsonPropertyName("sr")] public string? ScreenResolution { get; set; } // 1560x1440
  //
  // [JsonPropertyName("uaa")] public string? Arch { get; set; } // "arm";
  //
  // [JsonPropertyName("uab")] public string? ArchBit { get; set; } // "64";
  //
  // [JsonPropertyName("uafvl")] public string? UserAgentFullVersionList { get; set; } // "Chromium;124.0.6367.209|Google%20Chrome;124.0.6367.209|Not-A.Brand;99.0.0.0";
  //
  // [JsonPropertyName("uamb")] public int UserAgentMobile { get; set; } = 0;
  //
  // [JsonPropertyName("uam")] public string? UserAgentModel { get; set; } // "";
  //
  // [JsonPropertyName("uap")] public string? UserAgentPlatform { get; set; } // "macOS";
  //
  // [JsonPropertyName("uapv")] public string? UserAgentPlatformVersion { get; set; } // "14.5.0";
  //
  // [JsonPropertyName("uaw")] public int UserAgentWow64 { get; set; } // "64";
  //
  // [JsonIgnore] public IEventParams? EventParams { get; set; }
  //
  // [JsonIgnore] public IUserParams? UserParams { get; set; }
}

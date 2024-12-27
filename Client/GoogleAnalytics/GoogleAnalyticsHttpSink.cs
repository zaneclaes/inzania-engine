#region

using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using IZ.Core.Contexts;
using IZ.Core.Data;
using IZ.Core.Json;
using IZ.Core.Observability.Analytics;

#endregion

namespace IZ.Client.GoogleAnalytics;

public class GoogleAnalyticsHttpSink : LogicBase, IAnalyticsSink {
  private Dictionary<string, object> _userProps = new Dictionary<string, object>();
  private string _installId = ModelId.GenerateId();
  private string _sessionId = "";
  private string? _userId;

  public GoogleAnalyticsHttpSink(ITuneContext c) : base(c) { }

  private const string GA4ApiEndpoint = "https://www.google-analytics.com/mp/collect";

  private AnalyticsStream Stream => _stream ?? TuneGoogleAnalytics.StagingStream;
  private AnalyticsStream? _stream;

  protected string Url => $"{GA4ApiEndpoint}?measurement_id={Stream.MeasurementId}&api_secret={HttpUtility.UrlEncode(Stream.ApiSecret)}";

  private HttpClient Client => _client ??= new HttpClient();
  private HttpClient? _client;

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void GAEvent(string name, string json);
#endif

  public Task<bool> SendEvent(AnalyticsEvent e) {
#if UNITY_WEBGL && !UNITY_EDITOR
      try {
        GAEvent(e.Name, TuneJson.SerializeObject(e.Params));
        return Task.FromResult(true);
      } catch (Exception ex) {
        Log.Error(ex, "Failed to send event {name} {@params}", e.Name, e.Params);
        return Task.FromResult(false);
      }
#else
    var req = new GaParams(_installId, _userId, _userProps);
    req.Events.Add(e);
    string json = TuneJson.SerializeObject(req);
    return SendRequest(json);
#endif
  }

  public Task Config(AnalyticsStream stream, string installId, string sessionId, string? userId, Dictionary<string, object>? userProps = null) {
    _stream = stream;
    _client = null;
    _userId = userId;
    _installId = installId;
    _sessionId = sessionId;
    if (userProps != null) _userProps = userProps;
    return Task.CompletedTask;
  }

  protected virtual async Task<bool> SendRequest(string? json = null) {
    // Log.Information("[GA] JSON {json}", json);

    var res = await Client.PostAsync(Url, json == null ? null : new StringContent(json, Encoding.UTF8, "application/json"));

    // Log.Information("[GA] {mid} {cde} ? {ok} ({url})", Stream.MeasurementId, res.StatusCode, res.IsSuccessStatusCode, Client.BaseAddress);
    return res.IsSuccessStatusCode;
  }
}

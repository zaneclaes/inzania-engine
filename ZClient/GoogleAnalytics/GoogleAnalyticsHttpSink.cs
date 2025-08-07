using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Web;
using IZ.Core.Contexts;
using IZ.Core.Data;
using IZ.Core.Json;
using IZ.Core.Observability.Analytics;
#region

#if Z_UNITY
using System.Runtime.InteropServices;
using Cysharp.Threading.Tasks;
using ZTask = Cysharp.Threading.Tasks.UniTask;
using Tasks = Cysharp.Threading.Tasks.UniTask;
#else
using ZTask = System.Threading.Tasks.Task;
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
using System;
#endif

#endregion

namespace IZ.Client.GoogleAnalytics;

public class GoogleAnalyticsHttpSink : LogicBase, IAnalyticsSink {
  private Dictionary<string, object> _userProps = new Dictionary<string, object>();
  private string _installId = ModelId.GenerateId();
  private string _sessionId = "";
  private string? _userId;

  public GoogleAnalyticsHttpSink(IZContext c) : base(c) { }

  private const string GA4ApiEndpoint = "https://www.google-analytics.com/mp/collect";

  private AnalyticsStream Stream => _stream ?? IzGoogleAnalytics.StagingStream;
  private AnalyticsStream? _stream;

  protected string Url => $"{GA4ApiEndpoint}?measurement_id={Stream.MeasurementId}&api_secret={HttpUtility.UrlEncode(Stream.ApiSecret)}";

  private HttpClient Client => _client ??= new HttpClient();
  private HttpClient? _client;

#if UNITY_WEBGL && !UNITY_EDITOR
  [DllImport("__Internal")]
  private static extern void GAEvent(string name, string json);
#endif

  public ZTask SendEvent(AnalyticsEvent e) {
#if UNITY_WEBGL && !UNITY_EDITOR
      try {
        GAEvent(e.Name, ZJson.SerializeObject(e.EventParams));
        return ZTask.CompletedTask;
      } catch (Exception ex) {
        Log.Error(ex, "Failed to send event {name} {@params}", e.Name, e.EventParams);
        return ZTask.CompletedTask;
      }
#else
    var req = new GaParams(_installId, _userId, _userProps);
    req.Events.Add(e);
    string json = ZJson.SerializeObject(req);
    return SendRequest(json);
#endif
  }

  public ZTask Config(AnalyticsStream stream, string clientId, string sessionId, string? userId, Dictionary<string, object>? userProps = null) {
    _stream = stream;
    _client = null;
    _userId = userId;
    _installId = clientId;
    _sessionId = sessionId;
    if (userProps != null) _userProps = userProps;
    return ZTask.CompletedTask;
  }

  protected virtual async ZTask SendRequest(string? json = null) {
    // Log.Information("[GA] JSON {json}", json);

    var res = await Client.PostAsync(Url, json == null ? null : new StringContent(json, Encoding.UTF8, "application/json"));

    // Log.Information("[GA] {mid} {cde} ? {ok} ({url})", Stream.MeasurementId, res.StatusCode, res.IsSuccessStatusCode, Client.BaseAddress);
    // return res.IsSuccessStatusCode;
  }
}

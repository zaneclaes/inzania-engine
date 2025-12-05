using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Web;
using IZ.Core;
using IZ.Core.Auth;
using IZ.Core.Contexts;
using IZ.Core.Data;
using IZ.Core.Json;
using IZ.Core.Observability.Analytics;
using IZ.Core.Utils;
#region

#if UNITY_WEBGL && !UNITY_EDITOR
using System;
using System.Runtime.InteropServices;
#endif

#endregion

namespace IZ.Client.GoogleAnalytics;

public class GoogleAnalyticsHttpSink : LogicBase, IAnalyticsSink {
  private Dictionary<string, object> _userProps = new Dictionary<string, object>();

  private static long SessionId = (long) ZEnv.Now.GetUnixTimestampSec();

  private string _clientId = ModelId.GenerateId();

  private IZIdentity? _userIdentity;

  public GoogleAnalyticsHttpSink(IZContext c) : base(c) { }

  private const string GA4ApiEndpoint = "https://www.google-analytics.com/mp/collect";
  private const string GA4ApiDebugEndpoint = "https://www.google-analytics.com/debug/mp/collect";
  private string Endpoint => _analyticsOptions?.Debug ?? false ?  GA4ApiDebugEndpoint : GA4ApiEndpoint;

  private AnalyticsOptions? _analyticsOptions;

  private Installation? _installation;

  protected string Url => $"{Endpoint}?measurement_id={_analyticsOptions?.MeasurementId}&api_secret={HttpUtility.UrlEncode(_analyticsOptions?.ApiSecret)}";

  private HttpClient Client => _client ??= CreateClient();
  private HttpClient? _client;

  private HttpClient CreateClient() {
    var httpClient = new HttpClient();

    httpClient.DefaultRequestHeaders.UserAgent.Clear();
    httpClient.DefaultRequestHeaders.UserAgent.Add(
      new ProductInfoHeaderValue(Context.App.ProductName, _installation?.Version ?? "0.0.0"));
    if (_installation != null) {
      httpClient.DefaultRequestHeaders.UserAgent.Add(
        new ProductInfoHeaderValue($"(Unity; {_installation.Os}; {_installation.Os})"));
    }
    return httpClient;
  }

  private string? GetAnalyticsUserId(IZUser? user) {
    if (user == null) return null;
    if (user.IsVisitor()) return null;
    return user.Id;
  }

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
    var req = new GaParams(_clientId, GetAnalyticsUserId(_userIdentity?.IZUser), _userProps);
    e.EventParams ??= new BaseParams();
    if (_installation != null) e.EventParams.LoadInstallation(_installation);
    e.EventParams.SessionId = SessionId;
    e.EventParams.SessionNumber = _installation?.LaunchNumber ?? 0;
    req.Events.Add(e);
    string json = ZJson.SerializeObject(req);
    return SendRequest(json);
#endif
  }
  public async ZTask Config(
    AnalyticsOptions options, Installation install, IZIdentity? identity = null, Dictionary<string, object>? userProps = null
  ) {
    _analyticsOptions = options;
    _client = null;
    _installation = install;
    _clientId = install.ClientId;
    await SetIdentity(identity, userProps);
  }

  public async ZTask SetIdentity(IZIdentity? identity = null, Dictionary<string, object>? userProps = null) {
    _userIdentity = identity;
    if (userProps != null) _userProps = userProps;
    _client?.Dispose();
    _client = null;
    await SendEvent(new AnalyticsEvent<BaseParams>("session_start", new BaseParams()));
  }

  protected virtual async ZTask SendRequest(string? json = null) {
    // Log.Information("[GA] JSON {json}", json);

    var res = await Client.PostAsync(Url, json == null ? null : new StringContent(json, Encoding.UTF8, "application/json"));

    // Log.Information("[GA] {cde} ? {ok} ({url})", res.StatusCode, res.IsSuccessStatusCode, Client.BaseAddress);
    // return res.IsSuccessStatusCode;
  }

  public override void Dispose() {
    _client?.Dispose();
    base.Dispose();
  }
}

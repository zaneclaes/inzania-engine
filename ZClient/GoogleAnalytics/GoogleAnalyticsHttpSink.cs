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

  private readonly HttpMessageHandler? _handler;

  /// <summary>POSTs that failed and were dropped, each logged once as a warning and never retried.</summary>
  public int DroppedSends => _droppedSends;
  private int _droppedSends;

  public GoogleAnalyticsHttpSink(IZContext c) : base(c) { }

  /// <summary>With the transport the POSTs go through (tests: a link that resets).</summary>
  public GoogleAnalyticsHttpSink(IZContext c, HttpMessageHandler handler) : base(c) {
    _handler = handler;
  }

  private const string GA4ApiEndpoint = "https://www.google-analytics.com/mp/collect";
  private const string GA4ApiDebugEndpoint = "https://www.google-analytics.com/debug/mp/collect";
  private string Endpoint => _analyticsOptions?.Debug ?? false ?  GA4ApiDebugEndpoint : GA4ApiEndpoint;

  private AnalyticsOptions? _analyticsOptions;

  private Installation? _installation;

  private AnalyticsTrafficStatus _trafficStatus = AnalyticsTrafficStatus.Unknown;

  protected string Url => $"{Endpoint}?measurement_id={_analyticsOptions?.MeasurementId}&api_secret={HttpUtility.UrlEncode(_analyticsOptions?.ApiSecret)}";

  private HttpClient Client => _client ??= CreateClient();
  private HttpClient? _client;

  private HttpClient CreateClient() {
    var httpClient = _handler == null ? new HttpClient() : new HttpClient(_handler, disposeHandler: false);

    httpClient.DefaultRequestHeaders.UserAgent.Clear();
    httpClient.DefaultRequestHeaders.UserAgent.Add(
      new ProductInfoHeaderValue(Context.App.ProductName, _installation?.Version ?? "0.0.0"));
    if (_installation != null) {
      httpClient.DefaultRequestHeaders.UserAgent.Add(
        new ProductInfoHeaderValue($"(Unity; {_installation.Os}; {_installation.Os})"));
    }
    return httpClient;
  }

  // Visitors get a user_id too: the visitor TuneUser row already exists, so anonymous -> signed-up
  // stays one GA journey instead of two unlinkable ones.
  private string? GetAnalyticsUserId(IZUser? user) => user?.Id;

#if UNITY_WEBGL && !UNITY_EDITOR
  [DllImport("__Internal")]
  private static extern void GAEvent(string name, string json);
#endif

  public ZTask SendEvent(AnalyticsEvent e) {
    if (_trafficStatus != AnalyticsTrafficStatus.External) return ZTask.CompletedTask;
#if UNITY_WEBGL && !UNITY_EDITOR
      try {
        // Stamped like the HTTP branch below, so engine-direct events from the WebGL runtime carry the
        // same install/session params as everything else.
        e.EventParams ??= new BaseParams();
        if (_installation != null) e.EventParams.LoadInstallation(_installation);
        if (e.EventParams.SessionId == 0) e.EventParams.SessionId = SessionId;
        e.EventParams.SessionNumber = _installation?.LaunchNumber ?? 0;
        GAEvent(e.Name, ZJson.SerializeObject(e.EventParams));
        return ZTask.CompletedTask;
      } catch (Exception ex) {
        // Warning, not Error: an Error log re-enters the app's error->analytics handler, so a
        // persistently failing GA endpoint would otherwise storm itself with exception events.
        Log.Warning(ex, "Failed to send event {name} {@params}", e.Name, e.EventParams);
        return ZTask.CompletedTask;
      }
#else
    var req = new GaParams(_clientId, GetAnalyticsUserId(_userIdentity?.IZUser), _userProps);
    e.EventParams ??= new BaseParams();
    if (_installation != null) e.EventParams.LoadInstallation(_installation);
    // An emitter that already stamped a session id (TuneAnalytics, which mirrors the same event into
    // UserEvent) keeps it — overwriting it here gave GA and the database different ids for one run.
    if (e.EventParams.SessionId == 0) e.EventParams.SessionId = SessionId;
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
    _trafficStatus = install.AnalyticsTrafficStatus;
    _clientId = install.ClientId;
    await SetIdentity(identity, userProps);
  }

  public ZTask SetIdentity(IZIdentity? identity = null, Dictionary<string, object>? userProps = null) {
    _userIdentity = identity;
    if (userProps != null) _userProps = userProps;
    _client?.Dispose();
    _client = null;
    // Browser/WebGL sessions belong to the page tag. Native Measurement Protocol still opens its own. It is sent, not
    // awaited: startup waits on this call (ClientContext's ready tasks), and it must not wait on telemetry over a slow
    // link. SendRequest never throws.
    if (_trafficStatus == AnalyticsTrafficStatus.External && _installation?.DeviceType != DeviceType.Browser)
      SendSessionStart().Forget();
    return ZTask.CompletedTask;
  }

  private async ZTask SendSessionStart() {
    try {
      await SendEvent(new AnalyticsEvent<BaseParams>("session_start", new BaseParams()));
    } catch (System.Exception e) {
      Log.Warning("[GA] session_start dropped: {why}", e.GetBaseException().Message);
    }
  }

  public ZTask SetTrafficStatus(AnalyticsTrafficStatus status) {
    _trafficStatus = status;
    return ZTask.CompletedTask;
  }

  /// <summary>
  /// Telemetry never fails the app. Staging 1416's `[START] fatal error!` was this POST: `Configure` →
  /// `SetIdentity` → `session_start` ran inside startup's ready tasks, and one `Connection reset by peer` from the
  /// analytics endpoint propagated up and failed startup. A failed send is a warning, never an error: an error log
  /// re-enters the app's error-to-analytics handler, and the smoke's console check fails on `[ERR]`. It is not retried,
  /// because a Measurement Protocol POST that did land would then count twice.
  /// </summary>
  protected virtual async ZTask SendRequest(string? json = null) {
    // Log.Information("[GA] JSON {json}", json);
    try {
      using var res = await Client.PostAsync(Url, json == null ? null : new StringContent(json, Encoding.UTF8, "application/json"));
    } catch (System.Exception e) {
      System.Threading.Interlocked.Increment(ref _droppedSends);
      Log.Warning("[GA] send failed, dropped: {why}", e.GetBaseException().Message);
    }

    // Log.Information("[GA] {cde} ? {ok} ({url})", res.StatusCode, res.IsSuccessStatusCode, Client.BaseAddress);
    // return res.IsSuccessStatusCode;
  }

  public override void Dispose() {
    _client?.Dispose();
    base.Dispose();
  }
}

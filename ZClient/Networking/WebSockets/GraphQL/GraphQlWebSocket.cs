#region

using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using IZ.Client.Queries;
using IZ.Core;
using IZ.Core.Api.GraphQLWebSockets;
using IZ.Core.Contexts;
using IZ.Core.Data;
using IZ.Core.Exceptions;
using IZ.Core.Json;
using IZ.Core.Utils;

#endregion

namespace IZ.Client.Networking.WebSockets.GraphQL;

public class GraphQlWebSocket<TData> : TransientObject, IActivate, IGraphQlWebSocket<TData> where TData : class {

  private readonly Dictionary<string, string> _headers;

  private readonly Func<JsonElement, Task<TData>> _parser;

  private readonly GraphRequest _request;

  private readonly string _subProtocol = "graphql-ws";

  private readonly Uri _subscriptionUrl;

  private int _connectionAttempts;

  private string _socketId = "";
  private GqlWebSocketState _state = GqlWebSocketState.Ready;

  public GraphQlWebSocket(
    IZContext context, GraphRequest req, IGraphQLWebSocketDelegate<TData> del, Func<JsonElement, Task<TData>> parser, Dictionary<string, string>? headers = null
  ) : base(context) {
    Delegate = del;
    _parser = parser;
    _subscriptionUrl = new Uri(context.App.Gql.Replace("http", "ws"));
    _request = req;
    _headers = ZQueries.GetHeaders(context, headers);
    CreateSocket();
  }

  public IGraphQLWebSocketDelegate<TData> Delegate { get; set; }

  public DateTime LastHeartbeat { get; private set; } = ZEnv.Now;

  public bool HasEverConnected { get; private set; }

  private IWebSocket? Socket { get; set; }

  public bool IsActive => State == GqlWebSocketState.Subscribed;
  public GqlWebSocketState State {
    get => _state;
    private set {
      if (_state == value) return;
      Log.Debug("[GQL-WS] state change {old} => {new}", _state, value);
      _state = value;
      Delegate.OnGraphQLWebSocketState(_state);
    }
  }

  public void Update() {
    Socket?.DispatchMessageQueue();
  }

  public override void Dispose() {
    Disconnect();
    base.Dispose();
  }

  private void DisposeSocket() {
    if (Socket != null) {
      State = GqlWebSocketState.Disconnected;
      Socket.OnOpen -= HandleOpen;
      Socket.OnError -= HandleError;
      Socket.OnClose -= HandleClose;
      Socket.OnMessage -= HandleMessage;
      Log.Debug("[GQL-WS] socket disposed");
    }
    Socket = null;
  }

  private void CreateSocket() {
    DisposeSocket();
    Socket = ZQueries.CreateWebSocket(Context, _subscriptionUrl, _subProtocol, _headers);
    Socket.OnOpen += HandleOpen;
    Socket.OnError += HandleError;
    Socket.OnClose += HandleClose;
    Socket.OnMessage += HandleMessage;
  }

  private Task WaitUntil(GqlWebSocketState state) {
    Log.Debug("[GQL-WS] WaitUntil {state}", state);
    return Tasks.WaitUntil(() => {
      // Log.Debug("[GQL-WS] Wait: {State} != {state}", State, state);
      Update(); // send data while waiting...
      return State >= state;
    });
  }

  private void HandleOpen() {
    HandleOpen(_request).Forget();
  }

  private async Task HandleOpen(GraphRequest req) {
    Log.Debug("[GQL-WS] Opened; Initializing...");
    _connectionAttempts = 0;

    State = GqlWebSocketState.Connecting;
    var gqlPayload = new GraphQLWebSocketMessage {
      Type = "connection_init",
      Payload = new ZWebSocketConnectionPayload {
        Authorization = _headers[ZHeaders.Authorization],
        ClientId = _headers[ZHeaders.ClientId],
        ApplicationVersion = _headers[ZHeaders.ApplicationVersion],
        RequestId = _headers[ZHeaders.RequestId],
        Env = _headers[ZHeaders.Env]
      }
    };
    await Send(ZJson.SerializeObject(gqlPayload));
    await WaitUntil(GqlWebSocketState.Connected);

    Log.Debug("[GQL-WS] Connected; Subscribing...");
    await Send(_socketId, req);
    State = GqlWebSocketState.Subscribed;

    Log.Debug("[GQL-WS] subscribed");
    HasEverConnected = true;
  }

  private void HandleError(string error) {
    Log.Information($"[GQL-WS] Connection error {error}!");
  }

  private void HandleClose(WebSocketCloseCode code) {
    if (code == WebSocketCloseCode.Normal) State = GqlWebSocketState.Completed;
  }

  public async Task EnsureConnected() {
    if (State == GqlWebSocketState.Subscribed) return;
    await Reconnect();
  }

  public async Task Reconnect() {
    if (_connectionAttempts > 0)
      // Exponential back-off
      await Task.Delay(_connectionAttempts * _connectionAttempts * 1000);
    else
      // Disconnect(); // For WebGL, which stays "connected" despite disconnection
      CreateSocket();
    if (Socket == null) throw new NullReferenceException(nameof(Socket));
    _connectionAttempts++;
    Log.Information("[GQL-WS] re-connect #{count}", _connectionAttempts);
    await Socket.Connect();
  }

  private void HandleMessage(byte[] bytes) => DoHandleMessage(bytes).Forget();

  private async Task DoHandleMessage(byte[] bytes) {
    string? messageContents = Encoding.UTF8.GetString(bytes);
    Log.Debug("[GQL-WS] RES {msg}", messageContents);
    // JObject obj = JObject.Parse(message);
    var msg = ZJson.DeserializeObject<GraphQLWebSocketMessage>(Context, messageContents);
    if (msg == null) {
      Log.Warning("[GQL-WS] failed to parse incoming message: {contents}", messageContents);
      return;
    }

    if (msg.Type.Equals("connection_ack")) {
      State = GqlWebSocketState.Connected;
    } else if (msg.Type.Contains("error")) {
      throw new ApplicationException("The handshake failed. Error: " + messageContents);
    } else if (msg.Type.Equals("data")) {
      object payload = msg.Payload ?? throw new RemoteZException(Context, "No payload");
      // var jsonData = payload.GetProperty("data");
      TData? data = null;
      try {
        data = await _parser((JsonElement) payload); //  (TData?) GraphRequest.FromPayload(Context, typeof(TData), jsonData.ToString());
      } catch (Exception e) {
        Log.Error(e, "[GQL-WS] failed to parse {type} from {payloadType} {data}", typeof(TData).Name, payload.GetType(), payload.ToString());
      }

      // Log.Information("[GQL-WS] {type}: {@data}", typeof(TData).Name, data ?? (object)message);
      if (data != null) Delegate.OnGraphQLWebSocketData(data);
      else throw new InternalZException(Context, "No data object returned");
    } else if (msg.Type.Equals("ka")) {
      // NO-OP (keep-alive)
    } else {
      Log.Error("[GQL-WS] message: {message}", messageContents);
    }
    LastHeartbeat = ZEnv.Now;
  }

  private Task Send(string data) {
    if (Socket == null) throw new NullReferenceException(nameof(Socket));
    ArraySegment<byte> b = new ArraySegment<byte>(Encoding.ASCII.GetBytes(data));
    return Socket.Send(b.ToArray());
  }

  public Task Send(string id, GraphRequest req) => Send("{\"id\": \"" + id + "\", \"type\": \"start\", \"payload\": " + req.ToPayload() + "}");

  public async Task Connect(string id = "1") {
    if (Socket == null) CreateSocket();
    _socketId = id;
    Log.Debug("[GQL-WS] Connect");
    Socket!.Connect().Forget();
    await WaitUntil(GqlWebSocketState.Subscribed);
    Log.Debug("[GQL-WS] Subscribed: {id}", _request.Id);

    // return UniTask.CompletedTask;
// #if UNITY_WEBGL && !UNITY_EDITOR
//       Log.Information("GQL WS Synchronous Connect");
//       Socket.Connect().AsUniTask().Forget();
// #else
//       // await UniTask.SwitchToThreadPool();
//       Socket.Connect().AsUniTask().Forget();
//       // UniTask.Run(() => Socket.Connect().AsUniTask()).Forget();
// #endif
// await WaitUntil(SocketState.Subscribed);

    // Log.Information("GQL WS Subscribed");
  }

  private async Task CloseSocket(IWebSocket socket) {
    try {
      await socket.Close();
      Log.Information("[GQL-WS] Closed");
    } catch (Exception e) {
      // Known to close without handshake
      if (e.Message.Contains("without completing the close")) Log.Debug("[GQL-WS] failed to close");
      else Log.Warning(e, "[GQL-WS] failed to close");
    }
  }

  public void Disconnect() {
    var socket = Socket;
    DisposeSocket();
    if (socket?.State == WebSocketState.Connecting || socket?.State == WebSocketState.Open) CloseSocket(socket).Forget();
    Log.Debug("[GQL-WS] Disconnect: Disposed & Disconnected");
  }
}

#region

using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading.Tasks;
using IZ.Client.Networking.Sockets;
using IZ.Client.Queries;
using IZ.Core;
using IZ.Core.Contexts;
using IZ.Core.Data;
using IZ.Core.Exceptions;
using IZ.Core.Json;
using IZ.Core.Utils;

#endregion

namespace IZ.Client.Networking;

public class GraphQlSocket<TData> : TransientObject, IActivate where TData : class {

  private readonly Dictionary<string, string> _headers;

  private readonly GraphRequest _request;

  private readonly string _subscriptionUrl;

  private int _connectionAttempts;

  private string _socketId = "";
  private SocketState _state = SocketState.Ready;

  public GraphQlSocket(
    IZContext context, string subscriptionUrl, GraphRequest req, Dictionary<string, string> headers) : base(context) {
    _subscriptionUrl = subscriptionUrl;
    _request = req;
    _headers = headers;
    CreateSocket();
  }

  public DateTime LastHeartbeat { get; private set; } = ZEnv.Now;

  public bool HasEverConnected { get; private set; }
  public SocketState State {
    get => _state;
    set {
      if (_state == value) return;
      _state = value;
      OnState?.Invoke(this, this);
    }
  }

  private ISocket? Socket { get; set; }

  private Queue<TData> MessageQueue { get; } = new Queue<TData>();

  public bool IsActive => State == SocketState.Subscribed;

#pragma warning disable 67
  public event EventHandler<GraphQlSocket<TData>>? OnState;

  public event EventHandler<GraphQlSocket<TData>>? OnData;
#pragma warning restore 67

  private void DisposeSocket() {
    if (Socket != null) {
      State = SocketState.Disconnected;
      Socket.OnOpen -= HandleOpen;
      Socket.OnError -= HandleError;
      Socket.OnClose -= HandleClose;
      Socket.OnMessage -= HandleMessage;
    }
    Socket = null;
  }

  private void CreateSocket() {
    DisposeSocket();
    Socket = TuneQueries.CreateSocket(_subscriptionUrl, _headers);
    Socket.OnOpen += HandleOpen;
    Socket.OnError += HandleError;
    Socket.OnClose += HandleClose;
    Socket.OnMessage += HandleMessage;
  }

  private Task WaitUntil(SocketState state) {
    Log.Information("[WS] WaitUntil {state}", state);
    return Tasks.WaitUntilAsync(() => {
      // Log.Debug("[WS] Wait: {State} != {state}", State, state);
      Update(); // send data while waiting...
      return State >= state;
    });
  }

  private void HandleOpen() {
    HandleOpen(_request).Forget();
  }

  private async Task HandleOpen(GraphRequest req) {
    Log.Information("[WS] Opened; Initializing...");
    _connectionAttempts = 0;

    State = SocketState.Connecting;
    await Send("{\"type\":\"connection_init\"}");
    await WaitUntil(SocketState.Connected);

    Log.Information("[WS] Connected; Subscribing...");
    await Send(_socketId, req);
    State = SocketState.Subscribed;

    Log.Information("[WS] Sent Init");
    HasEverConnected = true;
  }

  private void HandleError(string error) {
    Log.Information($"[WS] Connection error {error}!");
  }

  private void HandleClose(SocketCloseCode code) {
    if (code == SocketCloseCode.Normal) State = SocketState.Completed;
  }

  public async Task EnsureConnected() {
    if (State == SocketState.Subscribed) return;
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
    Log.Information("[WS] re-connect #{count}", _connectionAttempts);
    await Socket.Connect();
  }

  private void HandleMessage(byte[] bytes) {
    string? message = Encoding.UTF8.GetString(bytes);
    // Log.Information("GQL RES WS {msg}", message);
    // JObject obj = JObject.Parse(message);
    Dictionary<string, object>? obj = ZJson.DeserializeObject<Dictionary<string, object>>(Context, message);

    string? subType = (string) obj!["type"]! ?? "";
    if (subType.Equals("connection_ack")) {
      State = SocketState.Connected;
    } else if (subType.Contains("error")) {
      throw new ApplicationException("The handshake failed. Error: " + message);
    } else if (subType.Equals("data")) {
      try {
        object? payload = obj["payload"] ?? throw new RemoteZException(Context, "No payload");
        var data = (TData?) GraphRequest.FromPayload(Context, typeof(TData), payload.ToString());

        // Log.Information("[WS] {type}: {@data}", typeof(TData).Name, data ?? (object)message);
        if (data != null) MessageQueue.Enqueue(data);
        else throw new InternalZException(Context, "No data object returned");
      } catch (Exception e) {
        Log.Error(e, "[WS] failed to parse {type} from {data}", typeof(TData).Name, obj.ToString());
      } finally {
        OnData?.Invoke(this, this);
      }
    } else if (subType.Equals("ka")) {
      // NO-OP
    } else {
      Log.Error("[WS] message: {message}", message);
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
    Log.Information("[WS] Connect");
    Socket!.Connect().Forget();
    await WaitUntil(SocketState.Subscribed);

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

  private async Task CloseSocket(ISocket socket) {
    try {
      await socket.Close();
      Log.Information("[WS] Closed");
    } catch (Exception e) {
      // Known to close without handshake
      if (e.Message.Contains("without completing the close")) Log.Information("[WS] failed to close");
      else Log.Warning(e, "[WS] failed to close");
    }
  }

  public void Disconnect() {
    var socket = Socket;
    DisposeSocket();
    if (socket?.State == WebSocketState.Connecting || socket?.State == WebSocketState.Open) CloseSocket(socket).Forget();
    Log.Information("[WS] Disconnect: Disposed & Disconnected");
  }

  public void Update() {
#if !UNITY_WEBGL || UNITY_EDITOR
    Socket?.DispatchMessageQueue();
#endif
  }

  public override void Dispose() {
    Disconnect();
    base.Dispose();
  }
}

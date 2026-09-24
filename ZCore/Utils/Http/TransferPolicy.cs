using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using IZ.Core.Contexts;
using IZ.Core.Observability.Logging;

namespace IZ.Core.Utils.Http;

/// <summary>
/// How a request survives a slow or broken link: the one owner for every client transfer, whether a GraphQL request
/// (the app's `UnityHttpConnection`, the site's `TuneQuery` client through <see cref="StallHandler" />) or an asset/URL
/// fetch (<see cref="SharedHttp" />, `BaseAssetProvider`, `UnityAssetProvider`).
///
/// A transfer is judged by progress, never by wall clock. A learner on a &lt;1 Mbps link must be able to use the software
/// (operator, 2026-09-23), so a response that keeps arriving is waited for however long it takes. A transfer that moves
/// no byte for <see cref="StallTimeout" /> is dead: a black-holed connection, which a transport with no deadline at all
/// waits on forever. A retry-safe transfer that dies, stalls, is reset (staging 1416: one `Connection reset by peer`
/// failed the app's startup) or gets a gateway error is retried (<see cref="MaxAttempts" />). The retry is announced
/// through <see cref="TransferStatus" /> and logged as a warning. Only a final failure is an error: the smoke's console
/// check fails on `[ERR]`.
///
/// A query or a GET is retry-safe. A mutation is retry-safe only when it is declared so with the reason, through
/// <see cref="DeclareIdempotent" />. Anything else may already have landed, so it is never replayed.
/// </summary>
public sealed class TransferPolicy {
  public static readonly TransferPolicy Default = new TransferPolicy();

  /// <summary>No byte sent or received for this long ends the attempt. It is not a total timeout: the clock restarts on
  /// every byte. Above the 2.4–7.3 s GraphQL round trips measured on the 104–212 KB/s link of 2026-09-23.</summary>
  public TimeSpan StallTimeout { get; set; } = TimeSpan.FromSeconds(30);

  /// <summary>Attempts for a retry-safe transfer, the first included.</summary>
  public int MaxAttempts { get; set; } = 3;

  private readonly Dictionary<string, string> _idempotent = new Dictionary<string, string>(StringComparer.Ordinal);

  /// <summary>Wait before attempt <paramref name="next" /> (2, 3, …): 2 s, then 4 s.</summary>
  public TimeSpan Backoff(int next) => TimeSpan.FromSeconds(Math.Min(8, 1 << Math.Max(1, next - 1)));

  /// <summary>Declares that replaying <paramref name="operation" /> is harmless, and why: an upsert on a unique key,
  /// a set to a value. The reason is the review point; it is not decoration.</summary>
  public TransferPolicy DeclareIdempotent(string operation, string why) {
    if (string.IsNullOrWhiteSpace(why)) throw new ArgumentException($"{operation}: an idempotency declaration needs its reason");
    _idempotent[operation] = why;
    return this;
  }

  /// <summary>Why <paramref name="operation" /> is declared idempotent, or null.</summary>
  public string? IdempotentBecause(string operation) => _idempotent.TryGetValue(operation, out var why) ? why : null;

  /// <summary>A read (a query, a GET) is always retry-safe; a write only when declared.</summary>
  public bool IsRetrySafe(string operation, bool isRead) => isRead || _idempotent.ContainsKey(operation);

  /// <summary>Whether a failed attempt is retried: only a transient failure, only when retry-safe, only while attempts
  /// remain. An answer the server gave (a GraphQL error, a 4xx) is final.</summary>
  public bool ShouldRetry(TransferFailure failure, bool retrySafe, int attempt) =>
    retrySafe && failure.Transient && attempt < MaxAttempts;

  /// <summary>
  /// Runs <paramref name="attempt" /> until it succeeds, fails for good, or runs out of attempts. Each retried attempt is
  /// a warning with its number, and is published as <see cref="TransferStatus" />, so a learner sees "retrying" instead
  /// of a silent wait. Only the final failure is an error. An attempt itself logs nothing above a warning.
  /// </summary>
  public async ZTask<T> RunAsync<T>(string operation, bool retrySafe, Func<int, ZTask<TransferAttempt<T>>> attempt,
    IZLogger? log = null, Func<TimeSpan, ZTask>? delay = null) {
    for (int n = 1;; n++) {
      var result = await attempt(n);
      if (result.Failure == null) {
        if (n > 1) TransferStatus.Report(null);
        return result.Value!;
      }
      if (!ShouldRetry(result.Failure, retrySafe, n)) {
        if (n > 1) TransferStatus.Report(null);
        log?.Error("[HTTP] {op} failed after {n} attempt(s): {why}", operation, n, result.Failure.Reason);
        throw result.Failure.ToException(operation, n);
      }
      var wait = Backoff(n + 1);
      log?.Warning("[HTTP] {op} attempt {n} failed ({why}); retrying in {s}s", operation, n, result.Failure.Reason, wait.TotalSeconds);
      TransferStatus.Report($"Slow connection — retrying ({n + 1} of {MaxAttempts})");
      if (delay != null) await delay(wait);
      else await ZTask.Delay(wait);
    }
  }
}

/// <summary>Why one attempt failed. <see cref="Transient" /> failures (the link, a gateway) are worth another try.</summary>
public sealed class TransferFailure {
  public string Reason { get; }
  public bool Transient { get; }
  public Exception? Exception { get; }

  private TransferFailure(string reason, bool transient, Exception? exception) {
    Reason = reason;
    Transient = transient;
    Exception = exception;
  }

  public static TransferFailure Stalled(TimeSpan quiet, long bytes) =>
    new TransferFailure($"no progress for {quiet.TotalSeconds:0}s after {bytes} bytes", true, null);

  public static TransferFailure Connection(string reason, Exception? e = null) => new TransferFailure(reason, true, e);

  /// <summary>An HTTP status: 408, 429 and the gateway family (502, 503, 504 — the ELB's answers when the pod or the
  /// link is slow) are transient; any other status is the server's answer and final.</summary>
  public static TransferFailure Status(int status, string reason, Exception? e = null) =>
    new TransferFailure(reason, status is 408 or 429 or 502 or 503 or 504, e);

  public static TransferFailure Final(string reason, Exception? e = null) => new TransferFailure(reason, false, e);

  /// <summary>A transport exception: a reset, a refused or dropped socket, a stall (<see cref="StallHandler" />) or an
  /// I/O error is transient; anything else is final.</summary>
  public static TransferFailure FromException(Exception e) {
    for (var x = (Exception?) e; x != null; x = x.InnerException) {
      if (x is HttpRequestException or IOException or SocketException or TimeoutException)
        return Connection(e.GetBaseException().Message, e);
    }
    return Final(e.Message, e);
  }

  public Exception ToException(string operation, int attempts) =>
    Exception ?? new IOException($"[HTTP] {operation} failed after {attempts} attempt(s): {Reason}");
}

/// <summary>One attempt's outcome: a value, or the failure.</summary>
public sealed class TransferAttempt<T> {
  public T? Value { get; }
  public TransferFailure? Failure { get; }

  private TransferAttempt(T? value, TransferFailure? failure) {
    Value = value;
    Failure = failure;
  }

  public static TransferAttempt<T> Ok(T value) => new TransferAttempt<T>(value, null);
  public static TransferAttempt<T> Failed(TransferFailure failure) => new TransferAttempt<T>(default, failure);
}

/// <summary>
/// Progress of one transfer: the byte count it has moved (sent plus received). Any increase is progress;
/// <see cref="Stalled" /> is true once none has come for the stall timeout. The clock is injectable for tests.
/// </summary>
public sealed class TransferWatch {
  private readonly TimeSpan _stall;
  private readonly Func<TimeSpan> _now;
  private TimeSpan _lastProgress;
  public long Bytes { get; private set; } = -1;

  public TransferWatch(TimeSpan stall, Func<TimeSpan>? now = null) {
    _stall = stall;
    if (now == null) {
      var clock = Stopwatch.StartNew();
      now = () => clock.Elapsed;
    }
    _now = now;
    _lastProgress = _now();
  }

  public TimeSpan Quiet => _now() - _lastProgress;

  /// <summary>Records the transfer's current byte count; returns true when it has stalled.</summary>
  public bool Observe(long bytes) {
    if (bytes > Bytes) {
      Bytes = bytes;
      _lastProgress = _now();
    }
    return Stalled;
  }

  public bool Stalled => Quiet >= _stall;
}

/// <summary>The notice a waiting screen shows while a transfer retries (the app's splash label), or null.</summary>
public static class TransferStatus {
  private static string? _notice;
  public static string? Notice => _notice;
  public static event Action<string?>? Changed;

  public static void Report(string? notice) {
    if (_notice == notice) return;
    _notice = notice;
    Changed?.Invoke(notice);
  }
}

/// <summary>
/// Ends an <see cref="HttpClient" /> request that stops moving instead of one that is merely slow. `HttpClient.Timeout`
/// is a total deadline (100 s by default) that aborts a response still arriving on a slow link, so a client using this
/// runs with no total timeout and the handler bounds the silence instead: the wait for the response headers, and every
/// gap between body chunks, by <see cref="TransferPolicy.StallTimeout" />. A stall surfaces as a
/// <see cref="TimeoutException" /> naming it, which <see cref="TransferFailure.FromException" /> calls transient. It does
/// not retry by itself: whether a request is safe to replay is its caller's to say.
/// </summary>
public sealed class StallHandler : DelegatingHandler {
  private readonly TransferPolicy _policy;

  public StallHandler(TransferPolicy? policy = null, HttpMessageHandler? inner = null) {
    _policy = policy ?? TransferPolicy.Default;
    // Left unset for IHttpClientFactory (`AddHttpMessageHandler`), which assigns it and refuses one already set.
    if (inner != null) InnerHandler = inner;
  }

  protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
    var stall = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
    stall.CancelAfter(_policy.StallTimeout);
    HttpResponseMessage response;
    try {
      response = await base.SendAsync(request, stall.Token).ConfigureAwait(false);
    } catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) {
      stall.Dispose();
      throw new TimeoutException($"[HTTP] no response headers within {_policy.StallTimeout.TotalSeconds:0}s (stalled)");
    }
    stall.CancelAfter(Timeout.InfiniteTimeSpan);
    var body = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
    var content = new StreamContent(new StallStream(body, _policy.StallTimeout, stall));
    foreach (var header in response.Content.Headers) content.Headers.TryAddWithoutValidation(header.Key, header.Value);
    response.Content = content;
    return response;
  }

  /// <summary>A body stream whose every read must deliver within the stall timeout.</summary>
  private sealed class StallStream : Stream {
    private readonly Stream _inner;
    private readonly TimeSpan _stall;
    private readonly CancellationTokenSource _owner;

    public StallStream(Stream inner, TimeSpan stall, CancellationTokenSource owner) {
      _inner = inner;
      _stall = stall;
      _owner = owner;
    }

    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) {
      using var read = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _owner.Token);
      read.CancelAfter(_stall);
      try {
        return await _inner.ReadAsync(buffer, offset, count, read.Token).ConfigureAwait(false);
      } catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) {
        _owner.Cancel(); // the request's own token: abort the transfer, not just this read
        throw new TimeoutException($"[HTTP] the response stopped arriving: no bytes for {_stall.TotalSeconds:0}s (stalled)");
      }
    }

    public override int Read(byte[] buffer, int offset, int count) => ReadAsync(buffer, offset, count, CancellationToken.None).GetAwaiter().GetResult();
    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
    public override void Flush() { }
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    protected override void Dispose(bool disposing) {
      if (disposing) {
        _inner.Dispose();
        _owner.Dispose();
      }
      base.Dispose(disposing);
    }
  }
}

/// <summary>
/// The one long-lived <see cref="HttpClient" /> for URL fetches (assets, resources), under <see cref="TransferPolicy" />.
/// It has no total deadline, a stall bound, and retries for a GET. It is shared rather than created per call: a
/// `new HttpClient()` per fetch pays a fresh connection each time, on the slow links where that costs most.
/// </summary>
public static class SharedHttp {
  private static readonly Lazy<HttpClient> Shared = new Lazy<HttpClient>(() => Create());

  public static HttpClient Client => Shared.Value;

  /// <summary>A client with no total timeout whose silence is bounded by <paramref name="policy" />.</summary>
  public static HttpClient Create(TransferPolicy? policy = null, HttpMessageHandler? inner = null) =>
    new HttpClient(new StallHandler(policy, inner ?? new HttpClientHandler())) { Timeout = Timeout.InfiniteTimeSpan };

  /// <summary>GETs <paramref name="url" /> to the end, retrying a reset, a stall or a gateway error.</summary>
  public static ZTask<byte[]> GetBytesAsync(string url, IZLogger? log = null, HttpClient? client = null, TransferPolicy? policy = null,
    Func<TimeSpan, ZTask>? delay = null) {
    var http = client ?? Client;
    return (policy ?? TransferPolicy.Default).RunAsync<byte[]>(url, retrySafe: true, async n => {
      try {
        using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
        if (!response.IsSuccessStatusCode)
          return TransferAttempt<byte[]>.Failed(TransferFailure.Status((int) response.StatusCode, $"HTTP {(int) response.StatusCode} from {url}"));
        return TransferAttempt<byte[]>.Ok(await response.Content.ReadAsByteArrayAsync());
      } catch (Exception e) {
        var failure = TransferFailure.FromException(e);
        log?.Warning("[HTTP] {url} attempt {n}: {why}", url, n, failure.Reason);
        return TransferAttempt<byte[]>.Failed(failure);
      }
    }, log, delay);
  }
}

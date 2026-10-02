#region

using System;
using IZ.Schema.Errors;
using Microsoft.AspNetCore.Http;

#endregion

namespace IZ.Server.Graphql;

/// <summary>
/// A cancellation is the client's when the request it belongs to was aborted by the client: Kestrel cancels
/// <see cref="HttpContext.RequestAborted" /> only when the connection closes, so a server-side timeout (an EF command
/// timeout, an <c>HttpClient</c> timeout, a linked token the server cancelled) leaves it unset and stays an Error.
/// </summary>
public sealed class HttpClientAbortSignal : IClientAbortSignal {
  private readonly IHttpContextAccessor _http;

  public HttpClientAbortSignal(IHttpContextAccessor http) { _http = http; }

  public bool IsClientAbort(Exception ex) => IsClientAbort(ex, _http.HttpContext);

  public static bool IsClientAbort(Exception ex, HttpContext? http) =>
    ex is OperationCanceledException && http is { RequestAborted.IsCancellationRequested: true };
}

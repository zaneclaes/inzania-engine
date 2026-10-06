#region

using System;
using System.Net;

#endregion

namespace IZ.Schema.Errors;

/// <summary>
/// Tells <see cref="GraphqlErrorFilter" /> whether an error is the caller hanging up: the request was cancelled by the
/// client (a closed tab, a dropped connection mid-upload), not by the server. ZSchema has no HTTP types, so the host
/// supplies it (`IZ.Server.Graphql.HttpClientAbortSignal` reads `HttpContext.RequestAborted`).
/// </summary>
public interface IClientAbortSignal {
  /// <summary>True only for a cancellation the client caused. A cancellation from the server's own timeout is not one.</summary>
  bool IsClientAbort(Exception ex);

  /// <summary>A typed malformed-request refusal supplied by the HTTP host, independently of client cancellation.</summary>
  HttpStatusCode? GetClientRefusalStatus(Exception ex) => null;
}

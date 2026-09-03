#region

using System;
using System.Linq;

#endregion

namespace IZ.Client.Queries;

/// <summary>
/// Reading a failed request for what it actually says.
///
/// The distinction that matters to a client is **did the server answer**. A
/// <see cref="GraphException" /> means it did, and described the problem; anything else — a
/// `RemoteZException` wrapping an `HttpRequestException`, a cancellation, a DNS failure — means the
/// request never got an answer, and therefore says nothing about whether the session is valid.
///
/// Conflating the two logged people out: `ClientContext.RestoreSession` treated *any* exception as
/// "your identity is no good", so a single 504 from `currentSession` cleared the session and, on web,
/// redirected the visitor to the login page mid-interaction.
/// </summary>
public static class GraphErrors {
  private static readonly string[] AuthMarkers = {
    "auth", "unauthorized", "unauthenticated", "forbidden", "expired", "invalid_token",
  };

  /// <summary>
  /// True only when the server answered and the answer was about authorization. Deliberately
  /// conservative: everything unrecognized is a transport failure, because keeping a session that
  /// turned out to be stale is recoverable and dropping one that was fine is not.
  ///
  /// It does not look at `HttpRequestException.StatusCode` — netstandard2.0, which Unity compiles
  /// this against, has no such property.
  /// </summary>
  public static bool IsIdentityRejection(Exception? e) {
    for (var x = e; x != null; x = x.InnerException) {
      if (!(x is GraphException graph)) continue;
      return graph.Errors.Any(err => Mentions(err.Extensions?.Code) || Mentions(err.Extensions?.Reason));
    }
    return false;
  }

  private static bool Mentions(string? value) => !string.IsNullOrWhiteSpace(value) &&
    AuthMarkers.Any(m => value!.IndexOf(m, StringComparison.OrdinalIgnoreCase) >= 0);
}

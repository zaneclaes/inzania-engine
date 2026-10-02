#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using HotChocolate;
using IZ.Core.Contexts;
using IZ.Core.Exceptions;
using IZ.Core.Observability.Logging;

#endregion

namespace IZ.Schema.Errors;

public class GraphqlErrorFilter : ExceptionStatusCodes, IErrorFilter {

  /// <summary>HotChocolate's own refusal codes, raised with no exception by `[Authorize]`/`[ApiAuthorize]`.</summary>
  private static readonly HashSet<string> RefusalCodes = new HashSet<string>(StringComparer.Ordinal) {
    "AUTH_NOT_AUTHORIZED", "AUTH_NOT_AUTHENTICATED"
  };

  /// <summary>The status codes that mean the caller asked for something it may not have.</summary>
  private static readonly HashSet<string> RefusalStatuses = new HashSet<string>(StringComparer.Ordinal) {
    nameof(HttpStatusCode.NotFound), nameof(HttpStatusCode.Unauthorized),
    nameof(HttpStatusCode.Forbidden), nameof(HttpStatusCode.NotAcceptable)
  };

  public GraphqlErrorFilter(IZLogger log) : base(log) { }

  public IError OnError(IError error) {
    var ex = error.Exception;
    if (ex != null) {
      error = error
          .WithCode(GetExceptionErrorCode(ex)) //
          .WithMessage(ex.Message)
          .SetExtension("Exception", ex.GetType().Name)
          .SetExtension("Method", ex.Data["method"])
        ;
      var level = IsRefusal(ex, error.Code) ? ZEventLevel.Warning : ZEventLevel.Error;
      if (ex is ZException zEx) {
        error = error.SetExtension("Reason", zEx.Reason);
        Log.Write(level, "[GQL] {method} returned {code} ({type}): {msg}", ex.Data["method"], error.Code, ex.GetType().Name, error.Message);
      } else {
        Log.Write(level, ex, "[GQL] {method} returned {code} ({type}): {msg}", ex.Data["method"], error.Code, ex.GetType().Name, error.Message);
      }
    } else {
      var path = error.Path is not null ? error.Path.ToString() : "null";
      string ext = error.Extensions?.Any() ?? false ?
        " [" + string.Join(", ", error.Extensions.Select(e => e.Key + ": " + e.Value)) + "]" : "";
      var level = IsRefusal(null, error.Code) ? ZEventLevel.Warning : ZEventLevel.Error;
      Log.Write(level, "[GQL] unknown error {code} for {path}: {msg}{ext}", error.Code, path, error.Message, ext);
    }
    return error;
  }

  /// <summary>
  /// A refusal the caller earned (a `4xx`: a wrong password, a missing row, an anonymous call to a gated field, a
  /// parameter the server rejects) logs at Warning; everything else is a server fault and stays at Error. A bare
  /// <see cref="InvalidOperationException" /> maps to `NotFound` for the client, but EF and LINQ raise it for real
  /// faults ("a second operation was started on this context", "Sequence contains no elements"), so it stays Error.
  /// </summary>
  public static bool IsRefusal(Exception? ex, string? code) {
    if (ex == null) return code != null && RefusalCodes.Contains(code);
    if (ex is ParameterZException or NotFoundZException) return true;
    if (ex is ZException) return false; // InternalZException, RemoteZException: the server's own failure.
    if (code == nameof(HttpStatusCode.NotFound) && ex.GetType() == typeof(InvalidOperationException)) return false;
    return code != null && RefusalStatuses.Contains(code);
  }
}

#region

using System;
using System.Linq;
using HotChocolate;
using IZ.Core.Contexts;
using IZ.Core.Observability.Logging;

#endregion

namespace IZ.Schema.Errors;

public class GraphqlErrorFilter : ExceptionStatusCodes, IErrorFilter {
  public GraphqlErrorFilter(ITuneLogger log) : base(log) { }

  private static readonly string[] Warnings = {
    "request execution", "database operation"
  };

  // public GraphqlErrorFilter(ITuneContext context) : base(context) { }

  public IError OnError(IError error) {
    var ex = error.Exception;
    var ext = (error.Extensions?.Any() ?? false) ?
      string.Join(", ", error.Extensions.Select(e => e.Key + ": " + e.Value)) : "";
    if (ex != null) {
      error = error
        .WithCode(GetExceptionErrorCode(ex))
        .WithMessage(ex.Message);
      Log.Error(ex, "[GQL] exception {code}: {msg} {ext}", error.Code, error.Message, ext);
    } else {
      Log.Error("[GQL] unknown error {code}: {msg} {ext}", error.Code, error.Message, ext);
    }
    return error;
  }

  public static bool IsWarning(IError err) {
    if (err.Exception is ArgumentException) return true;
    // if (err.Exception is ApiException) return true;
    return IsWarningMessage(err.Message);
  }

  public static bool IsWarningMessage(string message) => false; // Warnings.Any(message.ToLowerInvariant().Contains);
}

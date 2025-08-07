#region

using System.Linq;
using HotChocolate;
using IZ.Core.Contexts;
using IZ.Core.Exceptions;
using IZ.Core.Observability.Logging;

#endregion

namespace IZ.Schema.Errors;

public class GraphqlErrorFilter : ExceptionStatusCodes, IErrorFilter {

  private static readonly string[] Warnings = {
    "request execution", "database operation"
  };
  public GraphqlErrorFilter(IZLogger log) : base(log) { }

  // public GraphqlErrorFilter(IZContext context) : base(context) { }

  public IError OnError(IError error) {
    var ex = error.Exception;
    if (ex != null) {
      error = error
          .WithCode(GetExceptionErrorCode(ex)) //
          .WithMessage(ex.Message)
          .SetExtension("Exception", ex.GetType().Name)
          .SetExtension("Method", ex.Data["method"])
        ;
      if (ex is ZException zEx)
        error = error.SetExtension("Reason", zEx.Reason);
      Log.Error(ex, "[GQL] {method} returned {code} ({type}): {msg}", ex.Data["method"], error.Code, ex.GetType().Name, error.Message);
    } else {
      string ext = error.Extensions?.Any() ?? false ?
        "\n" + string.Join(", ", error.Extensions.Select(e => e.Key + ": " + e.Value)) : "";
      Log.Error("[GQL] unknown error {code}: {msg}{ext}", error.Code, error.Message, ext);
    }
    return error;
  }
  //
  // public static bool IsWarning(IError err) {
  //   // if (err.Exception is ArgumentException) return true;
  //   // if (err.Exception is ApiException) return true;
  //   return IsWarningMessage(err.Message);
  // }
  //
  // public static bool IsWarningMessage(string message) => false; // Warnings.Any(message.ToLowerInvariant().Contains);
}

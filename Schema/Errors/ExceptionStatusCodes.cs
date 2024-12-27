#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Authentication;
using IZ.Core.Contexts;
using IZ.Core.Observability.Logging;

#endregion

namespace IZ.Schema.Errors;

public class ExceptionStatusCodes : IHaveLogger {
  public ExceptionStatusCodes(ITuneLogger log) { Log = log; }
  public ITuneLogger Log { get; }

  public HttpStatusCode Default { get; } = HttpStatusCode.BadRequest;

  public Dictionary<HttpStatusCode, Func<Exception, bool>> Map { get; } = new Dictionary<HttpStatusCode, Func<Exception, bool>> {
    [HttpStatusCode.NotFound] = CheckType(typeof(KeyNotFoundException), typeof(InvalidOperationException)),
    [HttpStatusCode.Unauthorized] = CheckType(typeof(AuthenticationException), typeof(UnauthorizedAccessException)),
    [HttpStatusCode.Forbidden] = CheckType(typeof(AccessViolationException)),
    [HttpStatusCode.NotAcceptable] = CheckType(typeof(ArgumentException), typeof(ArgumentNullException)),
    [HttpStatusCode.NotImplemented] = CheckType(typeof(ApplicationException))
  };

  private static Func<Exception, bool> CheckType(params Type[] types) {
    return e => types.Any(t => e.GetType().IsAssignableTo(t));
  }

  public string? GetExceptionErrorCode(Exception ex) {
    foreach (var key in Map.Keys)
      if (Map[key].Invoke(ex))
        return key.ToString();
    return null;
  }

  // Convert the string codes into HttpStatusCodes and find the highest.
  public HttpStatusCode GetHighestErrorCode(IEnumerable<string?> codes) {
    return codes.Select(e => e != null && Enum.TryParse(e!, true, out HttpStatusCode code) ? code : Default)
      .Distinct()
      .Max();
  }

  public HttpStatusCode GetResponseErrorCode(IEnumerable<string?> codes) {
    return codes.Select(e => e != null && Enum.TryParse(e!, true, out HttpStatusCode code) ? (int) code >= 500 ? Default : code : Default)
      .Distinct()
      .Max();
  }
}

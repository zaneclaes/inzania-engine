using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Claims;
using System.Threading.Tasks;
using HotChocolate;
using HotChocolate.Execution;
using IZ.Core.Contexts;
using IZ.Core.Observability.Logging;

namespace IZ.Schema.Work;

public static class WorkExtensions {
  public static void LogErrors(this IOperationResult result, IZLogger log, HttpStatusCode code = HttpStatusCode.OK) {

    List<IError> errors = result.Errors?.ToList() ?? new List<IError>();
    log.LogErrors(errors, code);

  }
  public static void LogErrors(this IZLogger log, List<IError> errors, HttpStatusCode code = HttpStatusCode.OK) {
    // List<IError> errors = result.Errors?.ToList() ?? new List<IError>();
    if (errors.Any()) {
      // foreach (var err in errors) {
      //   var logLevel = GraphqlErrorFilter.IsWarning(err) ? LogEventLevel.Warning : LogEventLevel.Error;
      //   Log.Write(logLevel, err.Exception, "[GQL] HTTP [{code}] {class} {@error}", code, err.Exception?.GetType().Name, err);
      // }
      var logLevel = code == HttpStatusCode.OK ? ZEventLevel.Debug : ZEventLevel.Error;
      log.Write(logLevel, "[GQL] HTTP [{code}] errors {@errors}", code, errors);
    } else if (code != HttpStatusCode.OK) {
      log.Warning("[GQL] HTTP [{code}] would have failed silently?", code);
    }
  }

  // new UserState(TuneSystemIdentity.Singleton.Principal)

  public static Task<IOperationResult> ExecuteInternalApiCall(this OperationRequestBuilder req, IZContext context, ClaimsPrincipal claimsPrincipal) =>
    ExecuteInternalApiCall(req, context, new UserState(claimsPrincipal));

  public static async Task<IOperationResult> ExecuteInternalApiCall(this OperationRequestBuilder req, IZContext context, UserState userState) {
    // using var op = AddRequestSpan("SYS", nameof(ExecuteSystemApiCall));
    var executor = await context.ServiceProvider.GetRequestExecutorAsync(null, context.CancellationToken);
    var res = await executor.ExecuteAsync(req
        .SetGlobalState(WellKnownContextData.UserState, userState)
        .Build(),
      context.CancellationToken);

    var q = res.ExpectOperationResult();
    q.LogErrors(context.Log);
    return q;
  }
}

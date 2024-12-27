#region

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using IZ.Core.Contexts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog.Events;

#endregion

namespace IZ.Server.Requests;

public static class ApiExceptionMiddleware {

  // Serves as configuration for ApiBehaviorOptions that require this method signature for exception handling.
  // public static IActionResult HandleModelValidationError(ActionContext context) {
  //   return new ApiResponse(context.HttpContext.RequestServices.GetRequiredService<ITuneContext>(), null, new ApiResultErrors(context.ModelState.Keys.SelectMany(
  //     k => (context.ModelState[k]?.Errors ?? new ModelErrorCollection()).Select(
  //       e => new ApiResultError(e.ErrorMessage, ApiErrorTypes.Validation, k, e.Exception?.Data))
  //   ).ToArray()), context.HttpContext.BuildResultMeta<ApiResultMeta>()).ToJsonResult();
  // }

  public static Task Invoke(HttpContext http) => WriteResponse(http, false);

  public static void UseCustomErrors(this IApplicationBuilder app, IHostEnvironment environment) {
    if (environment.IsDevelopment()) app.Use(WriteDevelopmentResponse);
    else app.Use(WriteProductionResponse);
  }

  private static Task WriteDevelopmentResponse(HttpContext httpContext, Func<Task> next) => WriteResponse(httpContext, true);

  private static Task WriteProductionResponse(HttpContext httpContext, Func<Task> next) => WriteResponse(httpContext, false);

  private static Task WriteResponse(HttpContext httpContext, bool includeDetails) {
    var ex = httpContext.Features.Get<IExceptionHandlerFeature>()?.Error;
    if (ex == null) return Task.CompletedTask;

    // var context = httpContext.RequestServices.GetRequiredService<FurContext>();
    // var traceId = Activity.Current?.Id ?? httpContext.TraceIdentifier;
    // context.Log.Error(ex, "[HTTP-WRITE] Exception {traceId}", traceId);

    ApiResultErrors errors = new ApiResultErrors(ApiResultError.BuildExceptionError(ex));
    ApiResponse res = new ApiResponse(httpContext.RequestServices.GetCurrentContext(), null, errors, httpContext.BuildResultMeta<ApiResultMeta>());
    Dictionary<string, object>? value = res.ToDictionaryValue(includeDetails);

    return ApiResponse.Write(value, httpContext, res.GetHttpStatusCode());
  }

  public static LogEventLevel GetLogLevel(HttpContext httpContext, double d, Exception? ex) {
    var context = httpContext.RequestServices.GetCurrentContext();
    if (ex != null) {
      if (ex is OperationCanceledException) return LogEventLevel.Warning;
      context.Log.Information("[HTTP-STATUS] Exception {type} {message}", ex.GetType(), ex.Message);
      return LogEventLevel.Error;
    }

    // https://andrewlock.net/using-serilog-aspnetcore-in-asp-net-core-3-excluding-health-check-endpoints-from-serilog-request-logging/
    if (httpContext.Response.StatusCode > 499) {
      context.Log.Information("[HTTP-STATUS] {code} without exception", httpContext.Response.StatusCode);
      return LogEventLevel.Error;
    }

    if (httpContext.Request.Path.StartsWithSegments("/healthz", StringComparison.OrdinalIgnoreCase)) return LogEventLevel.Verbose;
    return LogEventLevel.Information;
  }
}

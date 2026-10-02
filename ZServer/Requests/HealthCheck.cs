#region

using System.Collections.Generic;
using System.Net.Mime;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Diagnostics.HealthChecks;

#endregion

namespace IZ.Server.Requests;

public class HealthCheck {
  /// <summary>
  /// The readiness endpoint. A check reports <see cref="HealthStatus.Degraded" /> for a state the host expects and
  /// will leave on its own (initialization still pending): the probe still answers 503, so no traffic is routed, but
  /// the health service logs it at Warning and <see cref="WriteResponse" /> marks the 503 as expected. A check that
  /// has genuinely failed reports <see cref="HealthStatus.Unhealthy" />, which stays an Error on both log lines.
  /// </summary>
  public static HealthCheckOptions Readiness() => new HealthCheckOptions {
    Predicate = check => check.Tags.Contains("readiness"),
    ResultStatusCodes = new Dictionary<HealthStatus, int> {
      [HealthStatus.Healthy] = StatusCodes.Status200OK,
      [HealthStatus.Degraded] = StatusCodes.Status503ServiceUnavailable,
      [HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable
    },
    ResponseWriter = WriteResponse
  };

  public static Task WriteResponse(HttpContext context, HealthReport result) {
    context.Response.ContentType = MediaTypeNames.Application.Json;
    if (result.Status == HealthStatus.Degraded) context.MarkExpectedUnavailable();

    // JObject json = new(
    //   new JProperty("status", result.Status.ToString()),
    //   new JProperty("results", new JObject(result.Entries.Select(pair =>
    //     new JProperty(pair.Key, new JObject(
    //       new JProperty("status", pair.Value.Status.ToString()),
    //       new JProperty("description", pair.Value.Description),
    //       new JProperty("data", new JObject(pair.Value.Data.Select(
    //         p => new JProperty(p.Key, p.Value))))))))));

    return context.Response.WriteAsync("ok");
  }
}

#region

using System.Net.Mime;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Diagnostics.HealthChecks;

#endregion

namespace IZ.Server.Requests;

public class HealthCheck {
  public static Task WriteResponse(HttpContext context, HealthReport result) {
    context.Response.ContentType = MediaTypeNames.Application.Json;

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

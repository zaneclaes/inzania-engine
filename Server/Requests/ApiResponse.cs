using System.Collections.Generic;
using System.Linq;
using System.Net.Mime;
using System.Threading.Tasks;
using IZ.Core.Contexts;
using IZ.Core.Data;
using IZ.Core.Json;
using Microsoft.AspNetCore.Http;

namespace IZ.Server.Requests;

public class ApiResponse : TransientObject {
  private readonly object? _data;

  public ApiResponse(ITuneContext context, object? data, ApiResultErrors? errors, ApiResultMeta? meta = null) : base(context) {
    Errors = errors ?? new ApiResultErrors();
    Meta = meta ?? new ApiResultMeta();
    _data = data;
  }

  public ApiResultMeta Meta { get; }

  private ApiResultErrors Errors { get; }

  // What key to return, i.e., "identity.me" (for RestQL)
  public string? ResponsePath { get; set; } = null;

  public ApiResultError? Error => Errors.GetMostSevereError();

  public object? Extract(params string[] keys) => Extract(_data as IReadOnlyDictionary<string, object?>, keys.ToList());

  private object? Extract(IReadOnlyDictionary<string, object?>? data, List<string> keys) {
    if (data == null || !keys.Any()) return data;
    string? key = keys.First();
    keys.RemoveAt(0);
    if (!data.ContainsKey(key)) return null;
    object? val = data[key];
    if (!keys.Any()) return val;
    return Extract(val as IReadOnlyDictionary<string, object?>, keys);
  }

  public Dictionary<string, object> ToDictionaryValue(bool includeDetails) {
    Dictionary<string, object> res = new Dictionary<string, object>();
    if (Errors.Any()) {
      res.Add("errors", Errors.Select(e => e.ToApiResponseData(includeDetails)));
    } else {
      object? data = string.IsNullOrWhiteSpace(ResponsePath) ? _data : Extract(ResponsePath.Split('.').ToArray());
      res.Add("data", data ?? new { });
    }
    if (Meta.Any()) res.Add("meta", Meta);
    return res;
  }

  internal static Task Write(object res, HttpContext context, int statusCode = StatusCodes.Status200OK) {
    context.Response.StatusCode = statusCode;
    context.Response.ContentType = MediaTypeNames.Application.Json;
    string? jobj = TuneJson.SerializeObject(res);
    return context.Response.WriteAsync(jobj);
  }
}

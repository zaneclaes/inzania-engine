#region

using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;

#endregion

namespace IZ.Server.Requests;

public static class ApiExtensions {
  public static int GetHttpStatusCode(this ApiResponse apiResponse) {
    var error = apiResponse.Error;
    if (error == null) return StatusCodes.Status200OK;

    if (error?.Type == ApiErrorTypes.Exception) return StatusCodes.Status500InternalServerError;
    if (error?.Type == ApiErrorTypes.Unauthorized) return StatusCodes.Status401Unauthorized;
    if (error?.Type == ApiErrorTypes.Validation) return StatusCodes.Status403Forbidden;
    if (error?.Type == ApiErrorTypes.BadRequest) return StatusCodes.Status400BadRequest;
    return StatusCodes.Status500InternalServerError;
  }

  // public static JsonResult ToJsonResult(this ApiResponse apiResponse) {
  //   return new JsonResult(apiResponse.ToDictionaryValue(apiResponse.Context.App.Env != TuneEnvironment.Production)) {
  //     StatusCode = apiResponse.GetHttpStatusCode()
  //   };
  // }

  public static TMeta BuildResultMeta<TMeta>(this HttpContext http, TMeta? meta = null)
    where TMeta : Dictionary<string, object> {
    meta ??= new ApiResultMeta() as TMeta;
    if (meta == null) throw new ArgumentException($"Invalid meta type {typeof(TMeta)}");
    meta.TryAdd(ApiResultMeta.TraceIdKey, http.TraceIdentifier);
    return meta;
  }
}

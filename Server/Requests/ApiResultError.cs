#region

using System;
using System.Collections;
using System.Collections.Generic;
using IZ.Core.Utils;

#endregion

namespace IZ.Server.Requests;

public enum ApiErrorTypes {
  Exception = 0,
  Unauthorized,
  BadRequest,
  Validation
}

public class ApiResultError {
  public ApiResultError(string message, ApiErrorTypes type, string? code = null, IDictionary? data = null) {
    Message = message;
    Type = type;
    Code = code;
    Data = data;
  }

  public string Message { get; }
  public ApiErrorTypes Type { get; }
  public string? Code { get; }
  public IDictionary? Data { get; }

  public IReadOnlyDictionary<string, object?>? Extensions { get; } = default!;
  // public IReadOnlyList<Location>? Locations { get; private set; }
  // public Path? Path { get; set; }
  public Exception? Exception { get; set; }
  public string? StackTrace { get; set; }

  public virtual Dictionary<string, object> ToApiResponseData(bool includeDetails) {
    // IZEnv.Log.Information("RESP");
    Dictionary<string, object> ret = new Dictionary<string, object> {
      ["message"] = Message,
      ["type"] = Type.ToString(),
      ["code"] = Code ?? "?"
    };
    if (Data != null) ret.Add("data", Data);
    if (Extensions != null) ret.Add("extensions", Extensions);
    if (Exception != null) ret.Add("exception", BuildExceptionError(Exception).ToApiResponseData(includeDetails));
    if (includeDetails && !string.IsNullOrEmpty(StackTrace)) ret.Add("stack", new TuneTrace(StackTrace));
    return ret;
  }

  public static ApiResultError BuildExceptionError(Exception e) => new ApiResultError(e.Message, ApiErrorTypes.Exception, e.GetType().Name, e.Data) {
    Exception = e.InnerException,
    StackTrace = e.StackTrace
  };

  public static ApiResultError BuildUnauthorizedError(string message, string code) => new ApiResultError(message, ApiErrorTypes.Unauthorized, code);
}

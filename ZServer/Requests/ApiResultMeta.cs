#region

using System.Collections.Generic;

#endregion

namespace IZ.Server.Requests;

public class ApiResultMeta : Dictionary<string, object> {
  public const string RedirectUrlKey = "redirectUrl";
  public const string TraceIdKey = "traceId";
  public const string OperationNameKey = "operationName";

  public static ApiResultMeta ForRedirectUrl(string? rUrl) => rUrl != null ? new ApiResultMeta {
    [RedirectUrlKey] = rUrl
  } : new ApiResultMeta();
}

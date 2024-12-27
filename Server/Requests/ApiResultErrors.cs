#region

using System.Collections.Generic;

#endregion

namespace IZ.Server.Requests;

// Simple wrapper for type convenience.
public class ApiResultErrors : List<ApiResultError> {
  public ApiResultErrors(params ApiResultError[] errors) {
    AddRange(errors);
  }

  public ApiResultError? GetMostSevereError() {
    ApiResultError? err = null;
    var type = ApiErrorTypes.BadRequest;
    for (int x = 0; x < Count; x++)
      if (err == null || this[x].Type < type) {
        type = this[x].Type;
        err = this[x];
      }
    return err;
  }
}

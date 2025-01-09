#region

using System.Collections;
using System.Collections.Generic;
using HotChocolate;
using IZ.Server.Requests;
using Path = HotChocolate.Path;

#endregion

namespace IZ.Server.Graphql;

public class GraphqlResultError : ApiResultError {

  public IReadOnlyList<Location>? Locations { get; set; }
  public Path? Path { get; set; }

  public GraphqlResultError(string message, ApiErrorTypes type, string? code = null, IDictionary? data = null) : base(message, type, code, data) { }

  public override Dictionary<string, object> ToApiResponseData(bool includeDetails) {
    Dictionary<string, object> ret = base.ToApiResponseData(includeDetails);
    if (Locations != null) ret.Add("locations", Locations);
    if (Path != null) ret.Add("path", Path);
    return ret;
  }

  public static GraphqlResultError BuildGraphQLError(IError e) => new GraphqlResultError(e.Message, ApiErrorTypes.Exception, e.Code) {
    Locations = e.Locations,
    Path = e.Path,
    Exception = e.Exception
  };
}

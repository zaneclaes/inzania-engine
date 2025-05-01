#region

using System;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using IZ.Core.Contexts;
using IZ.Core.Data;
using IZ.Core.Exceptions;
using IZ.Core.Json.System;
using StrawberryShake;

#endregion

namespace IZ.Client.Queries;

public abstract class GraphResult : TransientObject {
  public GraphResult(IZContext context, Response<JsonDocument> doc) : base(context) { }
}

public class GraphErrorExtensions {
  public string Code { get; set; } = null!;
}

public class GraphError {
  public string Message { get; set; } = null!;

  // [{"line":32,"column":3}]
  // public string Locations { get; set; } = null!;

  public object[] Path { get; set; } = null!;

  public GraphErrorExtensions Extensions { get; set; } = null!;

  public override string ToString() => $"{Message} ({string.Join(".", Path)})";
}

public class GraphResult<TData> : GraphResult {
  public TData Result { get; }

  public GraphResult(IZContext context, Response<JsonDocument> doc) : base(context, doc) {
    // TODO: P2 this may need some better error detection/handling
    if (doc.Exception != null) throw doc.Exception;
    if (doc.Body == null) {
      Log.Warning("[DOC] {@data}", doc.ContextData);
      throw new NullReferenceException(nameof(doc.Body));
    }
    if (doc.Body.RootElement.ValueKind != JsonValueKind.Object) {
      Log.Warning("[DOC] {@data}", doc.Body);
      throw new NullReferenceException("Root: " + doc.Body.RootElement.ValueKind.ToString());
    }
    GraphError[]? errors = null;
    if (doc.Body.RootElement.TryGetProperty("errors", out var errJson)) {
      if (errJson.ValueKind == JsonValueKind.Array) {
        errors = errJson.Deserialize<GraphError[]>(SystemJson.DeserializeOptionsForContext(Context));
        if (errors == null || errors.Length == 0) {
          Log.Warning("[DOC] empty graphQL errors {errors}", errJson);
        }
      } else {
        Log.Warning("[DOC] graphQL errors are {kind} {errors}", errJson.ValueKind, errJson);
      }
    }

    if (!doc.Body.RootElement.TryGetProperty("data", out var data) ||
        (data.ValueKind != JsonValueKind.Object && data.ValueKind != JsonValueKind.Array)) {
      if (errors == null || !errors.Any()) throw new NullReferenceException("NULL data with no errors? Data kind: " + data.ValueKind.ToString());
      throw new RemoteZException(context, string.Join("; ", errors.Select(e => e.ToString())));
    }

    if (errors is {Length: > 0})
      Log.Warning("[DOC] {count} non-fatal errors: {errors}", errors.Length, errors);

    var res = data.GetProperty("result");
    Result = res.Deserialize<TData>(SystemJson.DeserializeOptionsForContext(Context)) ??
             throw new ArgumentException($"Failed to deserialize graph result: {data}");
  }
}

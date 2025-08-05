#region

using System;
using System.Collections.Generic;
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
  public string? Code { get; set; }

  public string? Exception { get; set; }

  public string? StackTrace { get; set; }

  public string? Method { get; set; }

  public string? Reason { get; set; }
}

public class GraphError {
  public string Message { get; set; } = null!;

  // [{"line":32,"column":3}]
  // public string Locations { get; set; } = null!;

  public object[] Path { get; set; } = null!;

  public GraphErrorExtensions Extensions { get; set; } = null!;

  public string FormattedMessage {
    get {
      var msg = Message.Replace("\\n", "\n");
      if (msg.StartsWith("\\\"")) msg = msg.Substring(2);
      if (msg.StartsWith("\"")) msg = msg.Substring(1);
      if (msg.EndsWith("\\\"")) msg = msg.Substring(0, msg.Length - 2);
      if (msg.EndsWith("\"")) msg = msg.Substring(0, msg.Length - 1);
      return msg;
    }
  }

  public override string ToString() => $"{Message} ({string.Join(".", Path)})";
}

public class GraphException : RemoteZException {
  public string? OperationId { get; set; }

  public List<GraphError> Errors { get; }

  public List<GraphError> GetErrorsByReason(string reason) =>
    Errors.Where(e => e.Extensions.Reason == reason).ToList();

  public GraphException(IZContext context, params GraphError[] errors) : base(context,
    errors.Any() ? string.Join("\n", errors.Select(e => e.FormattedMessage)) : "Empty Error Set!") {
    Errors = errors.ToList();
  }
}

public class GraphResult<TData> : GraphResult {
  public TData Result { get; } = default!;

  public GraphResult(IZContext context, Response<JsonDocument> doc) : base(context, doc) {
    if (doc.Exception != null) throw doc.Exception;
    if (doc.Body == null) {
      Log.Warning("[GQL] {@data}", doc.ContextData);
      throw new NullReferenceException(nameof(doc.Body));
    }
    if (doc.Body.RootElement.ValueKind != JsonValueKind.Object) {
      Log.Warning("[GQL] {@data}", doc.Body);
      throw new NullReferenceException("Root: " + doc.Body.RootElement.ValueKind.ToString());
    }
    GraphError[]? errors = null;
    if (doc.Body.RootElement.TryGetProperty("errors", out var errJson)) {
      if (errJson.ValueKind == JsonValueKind.Array) {
        errors = errJson.Deserialize<GraphError[]>(SystemJson.DeserializeOptionsForContext(Context));
        if (errors == null || errors.Length == 0) {
          Log.Warning("[GQL] empty graphQL errors {errors}", errJson);
        }
      } else {
        Log.Warning("[GQL] graphQL errors are {kind} {errors}", errJson.ValueKind, errJson);
      }
    }

    var failedData = !doc.Body.RootElement.TryGetProperty("data", out var data) ||
                       (data.ValueKind != JsonValueKind.Object && data.ValueKind != JsonValueKind.Array);
    if (failedData || (errors?.Any() ?? false)) {
      if (errors == null || !errors.Any()) throw new NullReferenceException("NULL data with no errors? Data kind: " + data.ValueKind.ToString());
      var ex = new GraphException(context, errors);
      if (context.App.HandleZException(ex)) return;
      throw ex;
    }

    Log.Verbose("[GQL] data {json}", data);

    if (errors is {Length: > 0})
      Log.Warning("[GQL] {count} non-fatal errors: {errors}", errors.Length, errors);

    var res = data.GetProperty("result");
    Result = res.Deserialize<TData>(SystemJson.DeserializeOptionsForContext(Context)) ??
             throw new ArgumentException($"Failed to deserialize graph result: {doc.Body.RootElement}");
  }
}

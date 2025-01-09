#region

using System;
using System.Text.Json;
using IZ.Core.Contexts;
using IZ.Core.Data;
using IZ.Core.Json.System;
using StrawberryShake;

#endregion

namespace IZ.Client.Queries;

public abstract class GraphResult : TransientObject {
  public GraphResult(IZContext context, Response<JsonDocument> doc) : base(context) { }
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
    var data = doc.Body.RootElement.GetProperty("data");
    if (data.ValueKind != JsonValueKind.Object) {
      Log.Warning("[DOC] data: {data}", doc.Body.ToString());
      throw new NullReferenceException("Data: " + data.ValueKind.ToString());
    }
    var res = data.GetProperty("result");
    Result = res.Deserialize<TData>(SystemJson.DeserializeOptionsForContext(Context)) ??
             throw new ArgumentException("Failed to deserialize graph result");
  }
}

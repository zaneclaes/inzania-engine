#region

using System.Text.Json;
using IZ.Core.Contexts;
using IZ.Core.Observability.Logging;
using StrawberryShake;

#endregion

namespace IZ.Client.Queries;

public class GraphBuilder<TData> : IOperationResultBuilder<JsonDocument, GraphResult<TData>>, IHaveContext {
  public GraphBuilder(ITuneContext context) {
    Context = context;
    Log = context.Log.ForContext(GetType());
  }

  public IOperationResult<GraphResult<TData>> Build(Response<JsonDocument> response) => new OperationResult<GraphResult<TData>>(new GraphResult<TData>(Context, response),
    null, null!, null);
  public ITuneContext Context { get; }
  public ITuneLogger Log { get; }
}

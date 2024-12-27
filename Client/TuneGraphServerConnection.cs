#region

using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using IZ.Client.Queries;
using IZ.Core.Api;
using IZ.Core.Contexts;
using Microsoft.Extensions.DependencyInjection;
using StrawberryShake;
using StrawberryShake.Transport.Http;

#endregion

namespace IZ.Client;

public class TuneGraphServerConnection : LogicBase, IServerConnection {

  public async Task<TData> ExecuteApiRequest<TData>(ExecutionResult result, CancellationToken? ct = null) {
    var context = result.Context;
    var sp = context.ServiceProvider;
    OperationExecutor<JsonDocument, GraphResult<TData>>? opExecutor = new OperationExecutor<JsonDocument, GraphResult<TData>>(
      sp.GetRequiredService<IHttpConnection>(),
      () => new GraphBuilder<TData>(context),
      () => sp.GetRequiredService<IResultPatcher<JsonDocument>>(),
      sp.GetRequiredService<IOperationStore>());
    var execDoc = new GraphExecutionDocument(result);
    IOperationResult<GraphResult<TData>>? res = await opExecutor.ExecuteAsync(
      execDoc.ToOperationRequest(), ct ?? context.CancellationToken);

    if (res.Data == null) throw new NullReferenceException(result.Plan.OperationName);
    var data = res.Data!.Result;
    // context.Guard(data);
    Log.Debug("[API] {@data}", data);
    return data;
  }

  public TuneGraphServerConnection(ITuneContext context) : base(context) { }
}

#region

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using IZ.Client.Networking.WebSockets.GraphQL;
using IZ.Client.Queries;
using IZ.Core;
using IZ.Core.Api;
using IZ.Core.Api.GraphQLWebSockets;
using IZ.Core.Contexts;
using IZ.Core.Exceptions;
using Microsoft.Extensions.DependencyInjection;
using StrawberryShake;
using StrawberryShake.Transport.Http;

#endregion

namespace IZ.Client;

public class ZStubJsonConnection : IConnection<JsonDocument> {
  private readonly JsonDocument _doc;

  public ZStubJsonConnection(JsonElement element) {
    _doc = JsonDocument.Parse(element.GetRawText());
  }

  public IAsyncEnumerable<Response<JsonDocument>> ExecuteAsync(OperationRequest request) =>
    ToAsyncEnumerable(new Response<JsonDocument>(_doc, null));

  private IAsyncEnumerable<Response<JsonDocument>> ToAsyncEnumerable(Response<JsonDocument> item) {
    return SingleItem(item);

    static async IAsyncEnumerable<Response<JsonDocument>> SingleItem(Response<JsonDocument> item) {
      // Use `await Task.Yield()` to avoid the warning
      await Task.Yield();
      yield return item;
    }
  }
}

public class ZGraphServerConnection : LogicBase, IServerConnection {

  public ZGraphServerConnection() : base(ZEnv.SpawnRootContext()) { }
  public Task<TData> ExecuteApiRequest<TData>(ExecutionResult result, CancellationToken? ct = null) where TData : class =>
    ParseApiRequest<TData>(result.Context.ServiceProvider.GetRequiredService<IHttpConnection>(), result, ct);

  public async Task<IGraphQlWebSocket<TData>> Subscribe<TData>(ExecutionResult result, IGraphQLWebSocketDelegate<TData> del, CancellationToken? ct = null) where TData : class {
    var execDoc = new GraphExecutionDocument(result);
    var opReq = execDoc.ToOperationRequest();

    var graphReq = new GraphRequest {
      Id = opReq.Id!,
      // Query = string.IsNullOrWhiteSpace(),
      Variables = opReq.Variables //  req.Operation.VariablesNode ?????
    };
    GraphQlWebSocket<TData> cws = new GraphQlWebSocket<TData>(result.Context, graphReq, del, json =>
      ParseApiRequest<TData>(new ZStubJsonConnection(json), result, ct));
    await cws.Connect();
    return cws;
  }

  private async Task<TData> ParseApiRequest<TData>(
    IConnection<JsonDocument> connection, ExecutionResult result, CancellationToken? ct = null
  ) where TData : class {
    var context = result.Context;
    var sp = context.ServiceProvider;
    OperationExecutor<JsonDocument, GraphResult<TData>> opExecutor = new OperationExecutor<JsonDocument, GraphResult<TData>>(
      connection,
      () => new GraphBuilder<TData>(context),
      () => sp.GetRequiredService<IResultPatcher<JsonDocument>>(),
      sp.GetRequiredService<IOperationStore>());
    var execDoc = new GraphExecutionDocument(result);
    IOperationResult<GraphResult<TData>>? res;
    var opReq = execDoc.ToOperationRequest();
    try {
      res = await opExecutor.ExecuteAsync(opReq, ct ?? context.CancellationToken);
    } catch (GraphException e) {
      e.OperationId = opReq.Name;
      throw;
    } catch (RemoteZException) {
      throw;
    } catch (Exception e) {
      throw new RemoteZException(context, $"[GQL] Failed to execute {execDoc}", e);
    }

    if (res.Data == null) throw new NullReferenceException(nameof(TData));
    var data = res.Data!.Result;
    // context.Guard(data);
    Log.Debug("[API] {@data}", data);
    return data;
  }
}

#region

using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using IZ.Core.Api.GraphQLWebSockets;

#endregion

namespace IZ.Core.Api;

public interface IServerConnection {
  public Task<TData> ExecuteApiRequest<TData>(ExecutionResult result, CancellationToken? ct = null);

  public Task<IGraphQlWebSocket<TData>> Subscribe<TData>(ExecutionResult result, IGraphQLWebSocketDelegate<TData> del, CancellationToken? ct = null) where TData : class;
}

#region

using System.Threading;
using System.Threading.Tasks;

#endregion

namespace IZ.Core.Api;

public interface IGraphQlWebSocket<TData> where TData : class {

}

public interface IServerConnection {
  public Task<TData> ExecuteApiRequest<TData>(ExecutionResult result, CancellationToken? ct = null);

  public Task<IGraphQlWebSocket<TData>> Subscribe<TData>(ExecutionResult result, CancellationToken? ct = null) where TData : class;
}

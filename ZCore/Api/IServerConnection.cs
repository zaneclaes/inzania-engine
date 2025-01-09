#region

using System.Threading;
using System.Threading.Tasks;

#endregion

namespace IZ.Core.Api;

public interface IServerConnection {
  public Task<TData> ExecuteApiRequest<TData>(ExecutionResult result, CancellationToken? ct = null);
}

#region

using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using IZ.Core.Contexts;

#endregion

namespace IZ.Core.Data;

public interface IZQueryProvider : IQueryProvider, IHaveContext {
  IQueryProvider Provider { get; }

  public IZDataRepository Repository { get; }

  // Matches IAsyncQueryProvider from EF package
  public TResult ExecuteAsync<TResult>(Expression expression, CancellationToken cancellationToken = new CancellationToken());

}

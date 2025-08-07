#region

using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using IZ.Core.Contexts;
using IZ.Core.Data;
using IZ.Core.Observability.Logging;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.Internal;

#endregion

namespace IZ.Data.Resolvers;

 #pragma warning disable EF1001
public class ZEfCoreQueryProvider : EntityQueryProvider, IZQueryProvider {
  private readonly IAsyncQueryProvider _inner;

#pragma warning disable EF1001
  public ZEfCoreQueryProvider(IZContext context, IZDataRepository repo, IAsyncQueryProvider inner) : base(null!) {
#pragma warning restore EF1001
    Context = context;
    Log = context.Log;
    _inner = inner;
    Repository = repo;
    Provider = inner;
  }

  public IQueryProvider Provider { get; }

  public IZDataRepository Repository { get; }

  public IZContext Context { get; }

  public IZLogger Log { get; }

  public override IQueryable CreateQuery(Expression expression) => new DataModelQuery(this, _inner.CreateQuery(expression));
  public override IQueryable<TElement> CreateQuery<TElement>(Expression expression) => new DataModelQuery<TElement>(this, _inner.CreateQuery<TElement>(expression));
  public override object Execute(Expression expression) => _inner.Execute(expression)!;

  public override TResult Execute<TResult>(Expression expression) => _inner.Execute<TResult>(expression);

  public override TResult ExecuteAsync<TResult>(Expression expression, CancellationToken cancellationToken = new CancellationToken()) =>
    _inner.ExecuteAsync<TResult>(expression, cancellationToken);
 #pragma warning restore EF1001
}

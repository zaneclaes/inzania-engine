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

public class TuneEfCoreQueryProvider : EntityQueryProvider, ITuneQueryProvider {
  private readonly IAsyncQueryProvider _inner;

  public IQueryProvider Provider { get; }

  public ITuneDataRepository Repository { get; }

  public ITuneContext Context { get; }

  public ITuneLogger Log { get; }

#pragma warning disable EF1001
  public TuneEfCoreQueryProvider(ITuneContext context, ITuneDataRepository repo, IAsyncQueryProvider inner) : base(null!) {
#pragma warning restore EF1001
    Context = context;
    Log = context.Log;
    _inner = inner;
    Repository = repo;
    Provider = inner;
  }

  public override IQueryable CreateQuery(Expression expression) => new DataModelQuery(this, _inner.CreateQuery(expression));
  public override IQueryable<TElement> CreateQuery<TElement>(Expression expression) => new DataModelQuery<TElement>(this, _inner.CreateQuery<TElement>(expression));
  public override object Execute(Expression expression) => _inner.Execute(expression)!;

  public override TResult Execute<TResult>(Expression expression) => _inner.Execute<TResult>(expression);

  public override TResult ExecuteAsync<TResult>(Expression expression, CancellationToken cancellationToken = new CancellationToken()) =>
    _inner.ExecuteAsync<TResult>(expression, cancellationToken);
}

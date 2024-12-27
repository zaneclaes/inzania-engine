#region

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using IZ.Core.Api;
using IZ.Core.Contexts;
using IZ.Core.Data;
using Microsoft.EntityFrameworkCore.Query;

#endregion

// ReSharper disable NotDisposedResourceIsReturned

namespace IZ.Data.Storage;

public class TuneEfCoreRelationshipInclude<TEntity, TProperty> :
  LogicBase, IPreFetched<TEntity, TProperty> where TEntity : class {
  public ITuneDataRepository Repository { get; }
  public ITuneQueryProvider QueryProvider { get; }

  public IIncludableQueryable<TEntity, TProperty> EfQueryable { get; }

  // private readonly IEnumerator<TEntity> _enumerator;

  public TuneEfCoreRelationshipInclude(ITuneDataRepository repo, ITuneQueryProvider qp, IIncludableQueryable<TEntity, TProperty> q) : base(repo.Context) {
    Repository = repo;
    EfQueryable = q;
    QueryProvider = qp;
    // _enumerator = q.GetEnumerator();
  }
  public IEnumerator<TEntity> GetEnumerator() => EfQueryable.GetEnumerator();
  IEnumerator IEnumerable.GetEnumerator() => EfQueryable.GetEnumerator();
  public Type ElementType => EfQueryable.ElementType;
  public Expression Expression => EfQueryable.Expression;
  public IQueryProvider Provider => EfQueryable.Provider;
  public IAsyncEnumerator<TEntity> GetAsyncEnumerator(CancellationToken cancellationToken = new CancellationToken()) => (EfQueryable as IAsyncEnumerable<TEntity>)!.GetAsyncEnumerator(cancellationToken);
}

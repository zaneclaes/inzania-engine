#region

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;

#endregion

namespace IZ.Core.Data;

public class TuneQueryable<TData> : TransientObject, ITuneQueryable<TData> where TData : DataObject {

  private readonly IQueryable<TData> _q;

#if !TUNE_UNITY
  private readonly IAsyncEnumerable<TData> _asyncEnumerable;
    #endif

  public TuneQueryable(
    ITuneQueryProvider qp, IQueryable<TData> q
  ) : base(qp.Context) {
    _q = q;
    QueryProvider = qp;
#if !TUNE_UNITY
    _asyncEnumerable = q as IAsyncEnumerable<TData> ?? throw new ArgumentException($"{q.GetType().Name} is not IAsyncEnumerable");
#endif
  }

  public IEnumerator<TData> GetEnumerator() => _q.GetEnumerator();
  IEnumerator IEnumerable.GetEnumerator() => _q.GetEnumerator();
  public virtual Type ElementType => _q.ElementType;
  public Expression Expression => _q.Expression;
  public IQueryProvider Provider => QueryProvider;
  public ITuneDataRepository Repository => QueryProvider.Repository;
  public ITuneQueryProvider QueryProvider { get; }
#if !TUNE_UNITY
  public IAsyncEnumerator<TData> GetAsyncEnumerator(CancellationToken cancellationToken = new CancellationToken()) => _asyncEnumerable.GetAsyncEnumerator(cancellationToken);
#endif
}

#region

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using IZ.Core.Data;

#endregion

namespace IZ.Data.Resolvers;

public class DataModelQuery : TransientObject, ITuneQueryable {
  private readonly IQueryable _q;

  private readonly TuneEfCoreQueryProvider _provider;

  public DataModelQuery(TuneEfCoreQueryProvider provider, IQueryable q) : base(provider.Context) {
    _q = q;
    _provider = provider;
  }

  IEnumerator IEnumerable.GetEnumerator() => _q.GetEnumerator();
  public virtual Type ElementType => _q.ElementType;
  public Expression Expression => _q.Expression;
  public IQueryProvider Provider => _provider;
  public ITuneDataRepository Repository => _provider.Repository;
  public ITuneQueryProvider QueryProvider => _provider;
}

public class DataModelQuery<T> : DataModelQuery, ITuneQueryable<T> {
  private readonly IQueryable<T> _qt;

  public IEnumerator<T> GetEnumerator() => _qt.GetEnumerator();
  public DataModelQuery(TuneEfCoreQueryProvider provider, IQueryable<T> q) : base(provider, q) {
    _qt = q;
  }
  public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = new CancellationToken()) => (_qt as IAsyncEnumerable<T>)!.GetAsyncEnumerator(cancellationToken);
}

#region

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using IZ.Core.Contexts;
using IZ.Core.Exceptions;

#endregion

namespace IZ.Core.Data;

public abstract class DataCache : LogicBase {

  public DataCache(IZDataRepository repo) : base(repo.Context) {
    Repository = repo;
  }
  public IZDataRepository Repository { get; }

  public abstract void Cache(params object[] objects);
}

public class DataCache<T> : DataCache, IZQueryable<T> {
  private readonly List<T> _items = new List<T>();

  public DataCache(IZDataRepository repo) : base(repo) { }

  public IEnumerator<T> GetEnumerator() => _items.GetEnumerator();

  IEnumerator IEnumerable.GetEnumerator() => _items.GetEnumerator();

  public Type ElementType => typeof(T);

  public Expression Expression => _items.AsQueryable().Expression;

  public IQueryProvider Provider => _items.AsQueryable().Provider;

  public override void Cache(params object[] objects) {
    foreach (object? o in objects) {
      if (!(o is T obj)) throw new InternalZException(Context, $"Object {o.GetType().Name} is not a {typeof(T).Name}");
      _items.Add(obj);
    }
  }
  public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = new CancellationToken()) => throw new NotImplementedException();
  public IZQueryProvider QueryProvider { get; } = null!;
}

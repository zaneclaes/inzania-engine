#region

using System;
using System.Linq;
using System.Linq.Expressions;
using IZ.Core.Data;

#endregion

namespace IZ.Client.Queries;

public abstract class GraphQuery : GraphRequest {
  public IZDataRepository Repository { get; } = default!;

  public abstract Type ElementType { get; }

  public Expression Expression { get; } = default!;

  public IQueryProvider Provider { get; } = default!;
}

// public class GraphQuery<TData> : GraphQuery, ITuneQueryable<TData> {
//   private readonly List<TData> _arr = new();
//
//   public override Type ElementType => typeof(TData);
//
//   public IEnumerator<TData> GetEnumerator() {
//     return _arr.GetEnumerator();
//   }
//
//   IEnumerator IEnumerable.GetEnumerator() {
//     return _arr.GetEnumerator();
//   }
//   public IAsyncEnumerator<TData> GetAsyncEnumerator(CancellationToken cancellationToken = new CancellationToken()) {
//     throw new NotImplementedException();
//   }
//   public ITuneQueryProvider QueryProvider { get; } = default!;
// }

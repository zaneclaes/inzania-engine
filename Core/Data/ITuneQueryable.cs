#region

using System.Collections.Generic;
using System.Linq;
using IZ.Core.Contexts;

#endregion

namespace IZ.Core.Data;

public interface ITuneQueryable : IOrderedQueryable, IHaveContext {
  public ITuneDataRepository Repository { get; }

  public ITuneQueryProvider QueryProvider { get; }
}

public interface ITuneQueryable<out T> : IOrderedQueryable<T>, ITuneQueryable
#if !TUNE_UNITY
  , IAsyncEnumerable<T>
#endif
{ }

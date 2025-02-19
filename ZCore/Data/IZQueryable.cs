#region

using System.Collections.Generic;
using System.Linq;
using IZ.Core.Contexts;

#endregion

namespace IZ.Core.Data;

public interface IZQueryable : IOrderedQueryable, IHaveContext {
  public IZDataRepository Repository { get; }

  public IZQueryProvider QueryProvider { get; }
}

public interface IZQueryable<out T> : IOrderedQueryable<T>, IZQueryable
#if !Z_UNITY
  , IAsyncEnumerable<T>
#endif
{ }

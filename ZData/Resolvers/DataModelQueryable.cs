#region

using System.Linq;
using IZ.Core.Data;

#endregion

namespace IZ.Data.Resolvers;

public class DataModelQueryable<T> : ZQueryable<T> where T : DataObject {
  private readonly IQueryable<T> _db;

  public DataModelQueryable(IZQueryProvider qp, IQueryable<T> database) : base(qp, database) {
    _db = database;
  }
}

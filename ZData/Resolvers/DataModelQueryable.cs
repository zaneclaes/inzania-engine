#region

using IZ.Core.Data;
using Microsoft.EntityFrameworkCore;

#endregion

namespace IZ.Data.Resolvers;

public class DataModelQueryable<T> : ZQueryable<T> where T : DataObject {
  private readonly DbSet<T> _db;

  public DataModelQueryable(IZQueryProvider qp, DbSet<T> database) : base(qp, database) {
    _db = database;
  }
}

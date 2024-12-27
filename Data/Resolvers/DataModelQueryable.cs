#region

using IZ.Core.Data;
using Microsoft.EntityFrameworkCore;

#endregion

namespace IZ.Data.Resolvers;

public class DataModelQueryable<T> : TuneQueryable<T> where T : DataObject {
  private readonly DbSet<T> _db;

  public DataModelQueryable(ITuneQueryProvider qp, DbSet<T> database) : base(qp, database) {
    _db = database;
  }
}

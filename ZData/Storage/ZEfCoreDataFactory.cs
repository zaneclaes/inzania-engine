using IZ.Core.Contexts;
using IZ.Core.Data;

namespace IZ.Data.Storage;

public class ZEfCoreDataFactory<TDb> : IZDataFactory where TDb : ZDbContext {
  public IZDataRepository GetDataRepository(IZContext context) =>
    new ZEfCoreDataRepository<TDb>(context);
}

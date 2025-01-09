using IZ.Core.Contexts;

namespace IZ.Core.Data;

public interface IZDataFactory {
  public IZDataRepository GetDataRepository(IZContext context);
}

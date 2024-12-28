using IZ.Core.Contexts;

namespace IZ.Core.Data;

public interface ITuneDataFactory {
  public ITuneDataRepository GetDataRepository(ITuneContext context);
}

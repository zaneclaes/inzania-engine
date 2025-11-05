#region

using IZ.Core.Data.Attributes;
using IZ.Core.Observability;
using IZ.Core.Observability.Logging;

#endregion

namespace IZ.Core.Contexts;

public interface IHaveLogger : IGetLogged {
  [OutputIgnore]
  public IZLogger Log { get; }
}

public interface IHaveContext : IHaveLogger {
  [OutputIgnore]
  public IZContext Context { get; }
}

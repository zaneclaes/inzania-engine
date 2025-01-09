using System.Collections.Generic;
using IZ.Core.Contexts;
using IZ.Core.Data.Attributes;

namespace IZ.Core.Observability.Logging;

public interface IEventEnricher : IHaveContext {
  [ApiIgnore]
  public Dictionary<string, object> EventProperties { get; }

  public Dictionary<string, object> EventTags { get; }
}

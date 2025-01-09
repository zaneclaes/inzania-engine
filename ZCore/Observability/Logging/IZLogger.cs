using System;
using IZ.Core.Contexts;

namespace IZ.Core.Observability.Logging;

public interface IZLogger : IAmInternal {
  void Write(ZEventLevel level, string template, params object?[] args);

  void Write(ZEventLevel level, Exception e, string template, params object?[] args);

  IZLogger ForContext(Type context, IEventEnricher? enricher = null);
}

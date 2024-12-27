using System;
using IZ.Core.Contexts;

namespace IZ.Core.Observability.Logging;

public interface ITuneLogger : IAmInternal {
  void Write(TuneEventLevel level, string template, params object?[] args);

  void Write(TuneEventLevel level, Exception e, string template, params object?[] args);

  ITuneLogger ForContext(Type context, IEventEnricher? enricher = null);
}

#region

using IZ.Core.Observability.Logging;
using Serilog.Core;
using Serilog.Events;

#endregion

namespace IZ.Logging.SerilogLogging;

public class SerilogEnricher : ILogEventEnricher {
  private readonly IEventEnricher _eventEnricher;

  public SerilogEnricher(IEventEnricher eventEnricher) {
    _eventEnricher = eventEnricher;
  }

  public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory) { }
}

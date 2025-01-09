#region

using System;
using IZ.Core.Observability.Logging;
using Serilog;
using Serilog.Events;

#endregion

namespace IZ.Logging.SerilogLogging;

public class SerilogLogger : IZLogger {
  private readonly ILogger _logger;

  public SerilogLogger(ILogger logger) {
    _logger = logger;
  }

  private LogEventLevel GetLevel(ZEventLevel level) {
    if (level == ZEventLevel.Verbose) return LogEventLevel.Verbose;
    if (level == ZEventLevel.Debug) return LogEventLevel.Debug;
    if (level == ZEventLevel.Warning) return LogEventLevel.Warning;
    if (level == ZEventLevel.Error) return LogEventLevel.Error;
    if (level == ZEventLevel.Fatal) return LogEventLevel.Fatal;
    return LogEventLevel.Information;
  }

  public void Write(ZEventLevel level, string template, params object?[] args) =>
    _logger.Write(GetLevel(level), template, args);

  public void Write(ZEventLevel level, Exception e, string template, params object?[] args) =>
    _logger.Write(GetLevel(level), e, template, args);

  public IZLogger ForContext(Type context, IEventEnricher? enricher = null) {
    var logger = _logger.ForContext(context);
    if (enricher != null) logger = logger.ForContext(new SerilogEnricher(enricher));
    return new SerilogLogger(logger);
  }
}

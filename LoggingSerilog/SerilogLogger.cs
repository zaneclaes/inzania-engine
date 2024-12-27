#region

using System;
using IZ.Core.Observability.Logging;
using Serilog;
using Serilog.Events;

#endregion

namespace IZ.Logging.SerilogLogging;

public class SerilogLogger : ITuneLogger {
  private readonly ILogger _logger;

  public SerilogLogger(ILogger logger) {
    _logger = logger;
  }

  private LogEventLevel GetLevel(TuneEventLevel level) {
    if (level == TuneEventLevel.Verbose) return LogEventLevel.Verbose;
    if (level == TuneEventLevel.Debug) return LogEventLevel.Debug;
    if (level == TuneEventLevel.Warning) return LogEventLevel.Warning;
    if (level == TuneEventLevel.Error) return LogEventLevel.Error;
    if (level == TuneEventLevel.Fatal) return LogEventLevel.Fatal;
    return LogEventLevel.Information;
  }

  public void Write(TuneEventLevel level, string template, params object?[] args) =>
    _logger.Write(GetLevel(level), template, args);

  public void Write(TuneEventLevel level, Exception e, string template, params object?[] args) =>
    _logger.Write(GetLevel(level), e, template, args);

  public ITuneLogger ForContext(Type context, IEventEnricher? enricher = null) {
    var logger = _logger.ForContext(context);
    if (enricher != null) logger = logger.ForContext(new SerilogEnricher(enricher));
    return new SerilogLogger(logger);
  }
}

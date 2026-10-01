#region

using System;
using IZ.Core;
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

  public void Write(ZEventLevel level, string template, params object?[] args) =>
    _logger.Write(GetLevel(level), template, args);

  public void Write(ZEventLevel level, Exception e, string template, params object?[] args) {
    _logger.Write(GetLevel(level), e, template, args);
    try {
      // Logging does not decide the request outcome. The tracer records a caught span when one is installed.
      ZEnv.OnCaughtException?.Invoke(e, null);
    } catch (Exception) {
      // A telemetry failure must not replace the caller's exception.
    }
  }

  public IZLogger ForContext(Type context, IEventEnricher? enricher = null) {
    var logger = _logger.ForContext(context);
    if (enricher != null) logger = logger.ForContext(new SerilogEnricher(enricher));
    return new SerilogLogger(logger);
  }

  private LogEventLevel GetLevel(ZEventLevel level) {
    if (level == ZEventLevel.Verbose) return LogEventLevel.Verbose;
    if (level == ZEventLevel.Debug) return LogEventLevel.Debug;
    if (level == ZEventLevel.Warning) return LogEventLevel.Warning;
    if (level == ZEventLevel.Error) return LogEventLevel.Error;
    if (level == ZEventLevel.Fatal) return LogEventLevel.Fatal;
    return LogEventLevel.Information;
  }
}

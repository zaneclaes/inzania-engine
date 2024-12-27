using System;
using System.Collections.Generic;
using IZ.Core.Observability.Logging;
using Microsoft.Extensions.Logging;

namespace IZ.Logging.MicrosoftLogging;

public class MicrosoftLogger : ITuneLogger, IDisposable {
  private readonly ILoggerFactory _factory;

  private readonly ILogger _log;

  private MicrosoftLogger? _parent;

  private readonly IDisposable? _scope;

  public MicrosoftLogger(ILoggerFactory factory) {
    _factory = factory;
    _log = _factory.CreateLogger("root");
  }

  private MicrosoftLogger(MicrosoftLogger parent, Type context, IEventEnricher? enricher = null) {
    _factory = parent._factory;
    _log = parent._log;
    _parent = parent;

    List<object> args = new List<object> {
      "{context}"
    };
    List<string> template = new List<string> {
      context.Name
    };
    if (enricher != null) {
      foreach (KeyValuePair<string, object> kvp in enricher.EventProperties) {
        template.Add($"{{{kvp.Key}}}");
        args.Add(kvp.Value);
      }
    }

    _scope = _log.BeginScope(string.Join(" ", template), args.ToArray());
  }

  private LogLevel GetLevel(TuneEventLevel level) {
    if (level == TuneEventLevel.Verbose) return LogLevel.Trace;
    if (level == TuneEventLevel.Debug) return LogLevel.Debug;
    if (level == TuneEventLevel.Warning) return LogLevel.Warning;
    if (level == TuneEventLevel.Error) return LogLevel.Error;
    if (level == TuneEventLevel.Fatal) return LogLevel.Critical;
    return LogLevel.Information;
  }

  public void Write(TuneEventLevel level, string template, params object?[] args) =>
    _log.Log(GetLevel(level), template, args);

  public void Write(TuneEventLevel level, Exception e, string template, params object?[] args) =>
    _log.Log(GetLevel(level), e, template, args);

  public ITuneLogger ForContext(Type context, IEventEnricher? enricher = null) =>
    new MicrosoftLogger(this, context, enricher);

  public void Dispose() {
    _scope?.Dispose();
  }
}

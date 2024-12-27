using System;
using IZ.Core.Observability.Logging;
using Microsoft.Extensions.Logging;

namespace IZ.Logging.MicrosoftLogging;

public class MicrosoftLogBuilder : LogBuilder {
  private readonly ILoggerFactory _factory;

  private bool _console;

  public MicrosoftLogBuilder() {
    _factory = LoggerFactory.Create(Configure);
  }

  private void Configure(ILoggingBuilder builder) {
    if (_console) builder.AddConsole();
  }

  public override LogBuilder TransformObject<TObj>(Func<TObj, object> func) => this;

  public override LogBuilder TransformObjectWhere<TObj>(Func<Type, bool> pred, Func<TObj, object> func) => this;

  public override LogBuilder WriteToConsole() {
    _console = true;
    return this;
  }

  public override ITuneLogger BuildToSingleton() => new MicrosoftLogger(_factory);

  public override void Dispose() {
    _factory.Dispose();
  }
}

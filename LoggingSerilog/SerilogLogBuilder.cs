#region

using System;
using IZ.Core;
using IZ.Core.Observability.Logging;
using Serilog;
using Serilog.Configuration;

#endregion

namespace IZ.Logging.SerilogLogging;

public class SerilogLogBuilder : LogBuilder {

  public LoggerConfiguration SerilogConfig { get; private set; } = new LoggerConfiguration()
    .Destructure.ToMaximumDepth(10)
    .Enrich.FromLogContext();

  public override LogBuilder TransformObject<TObj>(Func<TObj, object> func) {
    SerilogConfig = SerilogConfig.Destructure.ByTransforming(func);
    return this;
  }

  public override LogBuilder TransformObjectWhere<TObj>(Func<Type, bool> pred, Func<TObj, object> func) {
    SerilogConfig = SerilogConfig.Destructure.ByTransformingWhere(pred, func);
    return this;
  }

  public override LogBuilder WriteToConsole() {
    SerilogConfig = SerilogConfig.WriteTo.Console();
    return this;
  }

  public SerilogLogBuilder WriteTo(Func<LoggerSinkConfiguration, LoggerConfiguration> func) {
    SerilogConfig = func(SerilogConfig.WriteTo);
    return this;
  }

  public SerilogLogBuilder ReadFrom(Func<LoggerSettingsConfiguration, LoggerConfiguration> func) {
    SerilogConfig = func(SerilogConfig.ReadFrom);
    return this;
  }

  public override ITuneLogger BuildToSingleton() => IZEnv.Log = new SerilogLogger(Log.Logger = SerilogConfig.CreateLogger());

  public override void Dispose() { }
}

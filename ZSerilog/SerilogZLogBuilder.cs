#region

using System;
using IZ.Core;
using IZ.Core.Data;
using IZ.Core.Observability.Logging;
using Serilog;
using Serilog.Configuration;
using Serilog.Events;

#endregion

namespace IZ.Logging.SerilogLogging;

public class SerilogZLogBuilder : ZLogBuilder {

  public LoggerConfiguration SerilogConfig { get; private set; } = new LoggerConfiguration()
    .Destructure.ToMaximumDepth(10)
    .Enrich.FromLogContext()
    // A seed conceding a row another replica inserted first: EF logs the failed save at Error, then the
    // repository recovers and reports it once as a warning (`DataRepositoryBase.IsConcededDuplicateKey`).
    .Filter.ByExcluding(e => e.Level >= LogEventLevel.Error && DataRepositoryBase.IsConcededDuplicateKey(e.Exception));
  public static SerilogZLogBuilder GetDefault() => new SerilogZLogBuilder().WithZData();

  public override ZLogBuilder TransformObject<TObj>(Func<TObj, object> func) {
    SerilogConfig = SerilogConfig.Destructure.ByTransforming(func);
    return this;
  }

  public override ZLogBuilder TransformObjectWhere<TObj>(Func<Type, bool> pred, Func<TObj, object> func) {
    SerilogConfig = SerilogConfig.Destructure.ByTransformingWhere(pred, func);
    return this;
  }

  public override ZLogBuilder WriteToConsole() {
    SerilogConfig = SerilogConfig.WriteTo.Console();
    return this;
  }

  public SerilogZLogBuilder WriteTo(Func<LoggerSinkConfiguration, LoggerConfiguration> func) {
    SerilogConfig = func(SerilogConfig.WriteTo);
    return this;
  }

  public SerilogZLogBuilder ReadFrom(Func<LoggerSettingsConfiguration, LoggerConfiguration> func) {
    SerilogConfig = func(SerilogConfig.ReadFrom);
    return this;
  }

  public override IZLogger BuildToSingleton() => ZEnv.Log = new SerilogLogger(Log.Logger = SerilogConfig.CreateLogger());

  public override IZLogger Build() => new SerilogLogger(SerilogConfig.CreateLogger());

  public override void Dispose() { }
}

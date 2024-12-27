#region

using System;
using Serilog;
using Serilog.Configuration;
using Serilog.Core;
using Serilog.Events;

#endregion

namespace IZ.Logging.SerilogLogging;

public static class SerilogExtensions {

  public static LoggerConfiguration Tuneality(
    this LoggerSinkConfiguration sinkConfiguration,
    LogEventLevel restrictedToMinimumLevel = LogEventLevel.Verbose,
    LoggingLevelSwitch? levelSwitch = null) {
    if (sinkConfiguration == null)
      throw new ArgumentNullException(nameof(sinkConfiguration));
    return sinkConfiguration.Sink(TuneLogSink.Singleton, restrictedToMinimumLevel, levelSwitch);
  }
}

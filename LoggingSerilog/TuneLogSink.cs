#region

using System.Collections.Generic;
using IZ.Core.Data.Attributes;
using Serilog.Core;
using Serilog.Events;

#endregion

namespace IZ.Logging.SerilogLogging;

[ApiDocs("Simple, pluggable sink that is ALWAYS added as a static singleton to Serilog")]
public class TuneLogSink : ILogEventSink {
  public static TuneLogSink Singleton = new TuneLogSink();

  private readonly List<ILogEventSink> _delegates = new List<ILogEventSink>();

  private TuneLogSink() { }

  public void Emit(LogEvent logEvent) {
    foreach (var sink in _delegates) sink.Emit(logEvent);
  }

  public void AddSink(ILogEventSink sink) {
    _delegates.Add(sink);
  }
}

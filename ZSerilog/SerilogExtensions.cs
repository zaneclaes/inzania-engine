#region

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using IZ.Core.Contexts;
using IZ.Core.Json;
using IZ.Core.Observability.Logging;
using Serilog.Events;
using Serilog.Formatting.Display;
using Serilog.Parsing;

#endregion

namespace IZ.Logging.SerilogLogging;

public static class SerilogExtensions {
  public static string RenderMessage(this LogFileEntry entry) {
    // Parse the message template
    var parser = new MessageTemplateParser();
    var messageTemplate = parser.Parse(entry.MessageTemplate);

    Dictionary<string, object>? props = ZJson.DeserializeObject<Dictionary<string, object>>(entry.Context, entry.Properties)!;

    // Convert dictionary to LogEventProperty list
    List<LogEventProperty> logProperties = props
      .Select(kvp => new LogEventProperty(kvp.Key, new ScalarValue(kvp.Value)))
      .ToList();

    // Create a dummy LogEvent (you can customize timestamp, level, etc.)
    var logEvent = new LogEvent(
      DateTimeOffset.Now,
      LogEventLevel.Information,
      null,
      messageTemplate,
      logProperties
    );

    // Format the message
    using var writer = new StringWriter();
    var formatter = new MessageTemplateTextFormatter("{Message}");
    formatter.Format(logEvent, writer);
    return writer.ToString();
  }

  public static List<LogEntry> ToLogEntries(this List<LogFileEntry> entries, string fn, string clientId, string? userId = null) {
    if (fn.Length > 255) fn = fn.Substring(0, 255);
    return entries.Select((e, i) => e.ToLogEntry(fn, clientId, userId, i)).ToList();
  }

  private static LogEntry ToLogEntry(this LogFileEntry entry, string fn, string clientId, string? userId, int lineNum) {
    string msgStr = entry.RenderMessage();
    if (msgStr.Length > LogEntry.MaxMessageLength) {
      entry.Log.Warning("[LOG] message {len} > {max}", msgStr.Length, LogEntry.MaxMessageLength);
      msgStr = msgStr.Substring(0, LogEntry.MaxMessageLength);
    }
    string? exStr = entry.Exception;
    if (exStr?.Length > LogEntry.MaxExceptionLength) {
      entry.Log.Warning("[LOG] exception {len} > {max}", exStr.Length, LogEntry.MaxExceptionLength);
      exStr = exStr.Substring(0, LogEntry.MaxExceptionLength);
    }
    string propStr = ZJson.SerializeObject(entry.Properties);
    if (propStr.Length > LogEntry.MaxPropsLength) {
      entry.Log.Warning("[LOG] properties {len} > {max}", propStr.Length, LogEntry.MaxPropsLength);
      propStr = propStr.Substring(0, LogEntry.MaxPropsLength);
    }
    return new LogEntry {
      Context = entry.Context,
      FileName = fn,
      ClientId = clientId,
      UserId = userId,
      LineNumber = lineNum,
      Message = msgStr,
      Exception = exStr,
      Properties = propStr,
      Level = entry.Level,
      LoggedAt = DateTimeOffset.Parse(entry.Timestamp).UtcDateTime
    };
  }
}

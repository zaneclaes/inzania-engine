#region

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using IZ.Core.Contexts;
using IZ.Core.Json;
using IZ.Core.Observability.Logging;
using Serilog;
using Serilog.Configuration;
using Serilog.Core;
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

    var props = ZJson.DeserializeObject<Dictionary<string, object>>(entry.Context, entry.Properties)!;

    // Convert dictionary to LogEventProperty list
    var logProperties = props
      .Select(kvp => {
        var scalar = new ScalarValue(kvp.Value);
        return new LogEventProperty(kvp.Key, scalar);
      })
      .ToList();

    // Create a dummy LogEvent (you can customize timestamp, level, etc.)
    var logEvent = new LogEvent(
      DateTimeOffset.Now,
      LogEventLevel.Information,
      exception: null,
      messageTemplate: messageTemplate,
      properties: logProperties
    );

    // Format the message
    using var writer = new StringWriter();
    var formatter = new MessageTemplateTextFormatter("{Message}", null);
    formatter.Format(logEvent, writer);
    return writer.ToString();
  }

  public static List<LogEntry> ToLogEntries(this List<LogFileEntry> entries, string fn, string installId, string? userId = null) {
    if (fn.Length > 255) fn = fn.Substring(0, 255);
    return entries.Select((e, i) => e.ToLogEntry(fn, installId, userId, i)).ToList();
  }

  private static LogEntry ToLogEntry(this LogFileEntry entry, string fn, string installId, string? userId, int lineNum) {
    var msgStr = entry.RenderMessage();
    if (msgStr.Length > LogEntry.MaxMessageLength) {
      entry.Log.Warning("[LOG] message {len} > {max}", msgStr.Length, LogEntry.MaxMessageLength);
      msgStr = msgStr.Substring(0, LogEntry.MaxMessageLength);
    }
    var propStr = ZJson.SerializeObject(entry.Properties);
    if (propStr.Length > LogEntry.MaxPropsLength) {
      entry.Log.Warning("[LOG] properties {len} > {max}", propStr.Length, LogEntry.MaxPropsLength);
      propStr = propStr.Substring(0, LogEntry.MaxPropsLength);
    }
    return new LogEntry() {
      Context = entry.Context,
      FileName = fn,
      InstallId = installId,
      UserId = userId,
      LineNumber = lineNum,
      Message = msgStr,
      Properties = propStr,
      Level = entry.Level,
      LoggedAt = DateTimeOffset.Parse(entry.Timestamp).UtcDateTime,
    };
  }
}

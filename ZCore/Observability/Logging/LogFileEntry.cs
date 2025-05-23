using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Threading.Tasks;
using IZ.Core.Contexts;
using IZ.Core.Data;
using IZ.Core.Json;

namespace IZ.Core.Observability.Logging;

// For deserializing from JSON
public class LogFileEntryJson : TransientObject {
  public string Timestamp { get; set; } = null!;

  public string Level { get; set; } = null!;

  public string MessageTemplate { get; set; } = null!;

  public Dictionary<string, object> Properties { get; set; } = null!;
}

// Since Dictionary<string, object> cannot be submitted via the API, we convert the JSON entry into one with a string
public class LogFileEntry : TransientObject {
  public string Timestamp { get; set; } = null!;

  public string Level { get; set; } = null!;

  public string MessageTemplate { get; set; } = null!;

  public string Properties { get; set; } = null!;

  private static LogFileEntry? FromLine(IZContext context, string line) {
    try {
      var json = ZJson.DeserializeObject<LogFileEntryJson>(context, line) ??
             throw new ArgumentException($"Deserialized line {line}");
      return new LogFileEntry {
        Context = context,
        Timestamp = json.Timestamp,
        Level = json.Level,
        MessageTemplate = json.MessageTemplate,
        Properties = ZJson.SerializeObject(json.Properties)
      };
    } catch (Exception e) {
      context.Log.Warning(e, "[LOG] failed to parse line {line}", line);
      return null;
    }
  }

  public static async Task<List<LogFileEntry>> LoadFromFile(IZContext context, string fp) {
    var txt = await System.IO.File.ReadAllTextAsync(fp);
    if (string.IsNullOrWhiteSpace(txt)) return new List<LogFileEntry>();
    var lines = txt.Split("}\n{");
    return lines.Select((l, i) => FromLine(context, (i > 0 ? "{" : "") + l + (i == lines.Length - 1 ? "" : "}")))
      .Where(l => l != null).Cast<LogFileEntry>().ToList();
  }
}

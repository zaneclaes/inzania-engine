#region

using System;
using System.IO;
using System.Linq;
using IZ.Core.Json;

#endregion

namespace IZ.Core.Observability.Logging;

public class ConsoleLogger : IZLogger {
  private readonly Type? _contextType;

  private readonly IEventEnricher? _eventEnricher;

  private readonly ConsoleLogger? _parent;

  private readonly TextWriter? _output;

  private readonly ZEventLevel _minimum;

  public ConsoleLogger(ConsoleLogger? parent = null, Type? type = null, IEventEnricher? enricher = null) {
    _parent = parent;
    _contextType = type;
    _eventEnricher = enricher;
  }

  /// <summary>A root logger that writes to <paramref name="output"/> and drops anything below
  /// <paramref name="minimum"/> — `ZScriptApp` uses stderr at Warning, so a script's stdout stays its output.</summary>
  public ConsoleLogger(TextWriter output, ZEventLevel minimum) {
    _output = output;
    _minimum = minimum;
  }

  private ConsoleLogger Root => _parent?.Root ?? this;

  private TextWriter Output => Root._output ?? Console.Out;

  public void Write(ZEventLevel level, string template, params object?[] args) {
    if (level < Root._minimum) return;
    Output.WriteLine($"[{level}] {template} {string.Join(", ", args.Select(PrintObject))}{Print()}");
  }

  public void Write(ZEventLevel level, Exception e, string template, params object?[] args) {
    if (level < Root._minimum) return;
    Output.WriteLine($"[{level}] {template} {string.Join(", ", args.Select(PrintObject))}{Print()}\n" +
                     $"{e.GetType().Name}: {e.Message}\n{string.Join("\n", e.StackTrace)}");
  }

  public IZLogger ForContext(Type context, IEventEnricher? enricher = null) =>
    new ConsoleLogger(this, context, enricher);

  private string PrintObject(object? o) {
    if (o == null) return "null";
    if (!(o is IGetLogged logged)) return o.ToString() ?? o.GetType().Name;
    return ZJson.SerializeObject(ZLogging.TransformObject<IGetLogged>(logged));
  }

  private string Print() {
    if (_contextType == null && _eventEnricher == null) return "";
    string ret = "";
    if (_contextType != null) ret += $"({_contextType.Name}) ";
    if (_eventEnricher != null) {
      ret += "{" + string.Join(", ", _eventEnricher.EventProperties.Select(p => $"\"{p.Key}\": {p.Value}")) + "}";
    }
    return " :: " + ret.Trim();
  }
}

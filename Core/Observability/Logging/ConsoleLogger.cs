#region

using System;
using System.Linq;
using IZ.Core.Json;

#endregion

namespace IZ.Core.Observability.Logging;

public class ConsoleLogger : ITuneLogger {
  private readonly Type? _contextType;

  private readonly IEventEnricher? _eventEnricher;

  private readonly ConsoleLogger? _parent;

  private ConsoleLogger Root => _parent?.Root ?? this;

  public ConsoleLogger(ConsoleLogger? parent = null, Type? type = null, IEventEnricher? enricher = null) {
    _parent = parent;
    _contextType = type;
    _eventEnricher = enricher;
  }

  public void Write(TuneEventLevel level, string template, params object?[] args) {
    Console.WriteLine($"[{level}] {template} {string.Join(", ", args.Select(PrintObject))}{Print()}");
  }

  public void Write(TuneEventLevel level, Exception e, string template, params object?[] args) {
    Console.WriteLine($"[{level}] {template} {string.Join(", ", args.Select(PrintObject))}{Print()}\n" +
                      $"{e.GetType().Name}: {e.Message}\n{string.Join("\n", e.StackTrace)}");
  }

  private string PrintObject(object? o) {
    if (o == null) return "null";
    if (!(o is IGetLogged logged)) return o.ToString();
    return TuneJson.SerializeObject(TuneLogging.TransformObject<IGetLogged>(logged));
  }

  private string Print() {
    if (_contextType == null && _eventEnricher == null) return "";
    string ret = "";
    if (_contextType != null) ret += $"({_contextType.Name}) ";
    if (_eventEnricher != null) {
      ret += "{" + string.Join(", ", _eventEnricher.EventProperties.Select(
        p => $"\"{p.Key}\": {p.Value}")) + "}";
    }
    return " :: " + ret.Trim();
  }

  public ITuneLogger ForContext(Type context, IEventEnricher? enricher = null) =>
    new ConsoleLogger(this, context, enricher);
}

#region

using System.Text.Json.Serialization;

#endregion

namespace IZ.Core.Observability.Analytics;

public interface IEventParams { }

public class AnalyticsEvent {
  [JsonPropertyName("name")] public string Name { get; set; }

  [JsonIgnore] public IEventParams? EventParams { get; set; }

  public AnalyticsEvent(string name, IEventParams? pars = null) {
    Name = name;
    EventParams = pars;
  }
}

public class NullParams : IEventParams { }

public class AnalyticsEvent<T> : AnalyticsEvent where T : IEventParams {
  [JsonPropertyName("params")] public T? Params { get; set; }

  public AnalyticsEvent(string name, T? pars) : base(name, pars) {
    Params = pars;
  }
}

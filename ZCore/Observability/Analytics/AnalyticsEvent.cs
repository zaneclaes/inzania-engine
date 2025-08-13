#region

using System.Text.Json.Serialization;

#endregion

namespace IZ.Core.Observability.Analytics;

public interface IEventParams {
  public long SessionId { get; set; }
}

public class AnalyticsEvent {

  public AnalyticsEvent(string name, IEventParams? pars = null) {
    Name = name;
    EventParams = pars;
  }
  [JsonPropertyName("name")] public string Name { get; set; }

  [JsonIgnore] public IEventParams? EventParams { get; set; }
}

public class BaseParams : IEventParams {
  [JsonPropertyName("session_id")] public long SessionId { get; set; }
}

public class AnalyticsEvent<T> : AnalyticsEvent where T : IEventParams {

  public AnalyticsEvent(string name, T? pars) : base(name, pars) {
    Params = pars;
  }
  [JsonPropertyName("params")] public T? Params { get; set; }
}

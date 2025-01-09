#region

using System.Text.Json.Serialization;
using IZ.Core.Observability.Analytics;

#endregion

namespace IZ.Client.GoogleAnalytics.Events;

public class SearchEventParams : IEventParams {
  [JsonPropertyName("search_term")] public string SearchTerm { get; set; } = default!;
}

#region

using System;
using System.Text.Json.Serialization;
using IZ.Core.Contexts;
using IZ.Core.Json;

#endregion

namespace IZ.Client.Queries;

public class GraphRequest {
  public static string graphQlTypenameKey = "__typename";

  public static string jsonTypenameKey = "$type";

  [JsonPropertyName("id")] public string Id { get; set; } = default!;

  // [JsonPropertyName("query")] public string? Query { get; set; }

  [JsonPropertyName("variables")] public object? Variables { get; set; }


  public string ToPayload() => TuneJson.SerializeObject(this);

  public static object? FromPayload(ITuneContext context, Type expectedType, string? text, string? key = null) => null;
}

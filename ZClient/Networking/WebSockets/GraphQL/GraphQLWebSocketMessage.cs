using System.Text.Json;

namespace IZ.Client.Networking.WebSockets.GraphQL;

public class GraphQLWebSocketMessage {
  public string Type { get; set; } = null!;

  public JsonElement? Payload { get; set; }
}

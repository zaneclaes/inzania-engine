namespace IZ.Client.Networking.WebSockets.GraphQL;

public class GraphQLWebSocketMessage {
  public string Type { get; set; } = null!;

  public object? Payload { get; set; }
}

namespace IZ.Core.Api.GraphQLWebSockets;

public class ZWebSocketConnectionPayload {
  public string? Authorization { get; set; }

  public string? InstallId { get; set; }

  public string? ApplicationVersion { get; set; }

  public string? RequestId { get; set; }

  public string? Env { get; set; }
}

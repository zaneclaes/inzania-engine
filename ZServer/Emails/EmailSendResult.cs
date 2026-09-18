using SendGrid;

namespace IZ.Server.Emails;

/// <summary>
/// Outcome of one provider attempt. <see cref="Accepted" /> is HTTP acceptance, not inbox
/// delivery. <see cref="Ambiguous" /> means the request may have been accepted (timeout after
/// the call started) and must not be retried blindly.
/// </summary>
public sealed class EmailSendResult {
  public Response? Response { get; init; }

  public bool Accepted { get; init; }

  public bool Ambiguous { get; init; }

  public bool Transient { get; init; }

  public int StatusCode { get; init; }

  public string? ProviderMessageId { get; init; }

  public static EmailSendResult Ok(Response response, string? messageId) => new() {
    Response = response,
    Accepted = true,
    StatusCode = (int) response.StatusCode,
    ProviderMessageId = messageId,
  };

  public static EmailSendResult Rejected(int status, bool transient, Response? response = null) => new() {
    Response = response,
    StatusCode = status,
    Transient = transient,
  };

  public static EmailSendResult MaybeAccepted() => new() {
    Ambiguous = true,
  };
}

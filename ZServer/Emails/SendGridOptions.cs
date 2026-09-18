using IZ.Core.Data.Attributes;

namespace IZ.Server.Emails;

public class SendGridOptions {
  public string SenderAddress { get; set; } = null!;
  public string SenderName { get; set; } = null!;
  public string? RecipientAddress { get; set; } = null!;
  public string? RecipientName { get; set; } = null!;
  [ApiSecret] public string Key { get; set; } = null!;
  [ApiSecret] public string ValidatorKey { get; set; } = null!;
  /// <summary>SendGrid Event Webhook verification public key (PEM). Empty until the operator
  /// configures the signed webhook; never a new committed secret.</summary>
  public string? EventWebhookPublicKey { get; set; }
  public SendGridTemplates Templates { get; set; } = null!;
}

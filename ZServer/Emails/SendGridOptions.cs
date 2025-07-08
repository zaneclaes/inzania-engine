namespace IZ.Server.Emails;

public class SendGridOptions {
  public string SenderAddress { get; set; } = null!;
  public string SenderName { get; set; } = null!;
  public string? RecipientAddress { get; set; } = null!;
  public string? RecipientName { get; set; } = null!;
  public string Key { get; set; } = null!;
  public string ValidatorKey { get; set; } = null!;
  public SendGridTemplates Templates { get; set; } = null!;
}

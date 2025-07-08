#region

using System.Threading.Tasks;
using IZ.Core.Contexts;
using Microsoft.AspNetCore.Identity.UI.Services;

#endregion

namespace IZ.Server.Emails;

public class EmailSender : LogicBase, IEmailSender {
  private readonly SendGridSender _sender;

  public EmailSender(SendGridSender sender) {
    _sender = sender;
  }

  // IEmailSender
  public Task SendEmailAsync(string email, string subject, string htmlMessage) => _sender.SendRawHtml(email, subject, htmlMessage);
}

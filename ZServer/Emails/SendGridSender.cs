#region

using System;
using System.Collections.Generic;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using IZ.Core;
using IZ.Core.Contexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using SendGrid;
using SendGrid.Helpers.Mail;

#endregion

namespace IZ.Server.Emails;

public abstract class SendGridSender : LogicBase {
  private SendGridClient? _api;
  private SendGridClient? _client;

  public EmailAddress SenderEmailAddress => _senderEmailAddress ??=
    string.IsNullOrWhiteSpace(_sendGridOpts.SenderAddress) ? throw new NullReferenceException(nameof(_sendGridOpts.SenderAddress)) :
    new EmailAddress(_sendGridOpts.SenderAddress, _sendGridOpts.SenderName);
  private EmailAddress? _senderEmailAddress;

  public EmailAddress RecipientEmailAddress => _recipAddress ??=
    string.IsNullOrWhiteSpace(_sendGridOpts.RecipientAddress) ? SenderEmailAddress :
    new EmailAddress(_sendGridOpts.RecipientAddress, _sendGridOpts.RecipientName);
  private EmailAddress? _recipAddress;

  private readonly SendGridOptions _sendGridOpts;

  private string SendGridKey => string.IsNullOrWhiteSpace(_sendGridOpts.Key) ? GetSendGridKeyEnv() : _sendGridOpts.Key;
  private string GetSendGridKeyEnv() {
    var key = $"SENDGRID_API_KEY_{Context.App.ProductName.ToUpperInvariant()}";
    var env = Environment.GetEnvironmentVariable(key);
    if (string.IsNullOrWhiteSpace(env)) throw new ArgumentException(nameof(SendGridKey));
    return env;
  }
  protected SendGridClient Client => _client ??= new SendGridClient(SendGridKey);

  private string ApiKey => string.IsNullOrWhiteSpace(_sendGridOpts.ValidatorKey) ? throw new ArgumentException(nameof(_sendGridOpts.ValidatorKey)) : _sendGridOpts.ValidatorKey;
  protected SendGridClient Api => _api ??= new SendGridClient(ApiKey);

  public abstract Task<EmailValidation?> ValidateEmailAsync(string email);

  protected SendGridSender(IZContext context, IOptions<SendGridOptions> opts) : base(context) {
    _sendGridOpts = opts.Value;
  }

  public Task<Response> SendTemplate(string email, string templateId, object args) {
    SendGridMessage msg = new SendGridMessage {
      From = SenderEmailAddress,
      TemplateId = templateId
    };
    msg.SetTemplateData(args);
    return Send(msg, email);
  }

  public Task<Response> SendRawHtml(string email, string subject, string message) => Send(new SendGridMessage {
    From = SenderEmailAddress,
    Subject = subject,
    PlainTextContent = message,
    HtmlContent = message
  }, email);

  public async Task<Response> Send(SendGridMessage msg, params string[] emails) {
    foreach (var email in emails)
      msg.AddTo(new EmailAddress(email));

    // Disable click tracking.
    // See https://sendgrid.com/docs/User_Guide/Settings/tracking.html
    msg.SetClickTracking(false, false);

    var res = await Client.SendEmailAsync(msg);
    string response = await res.Body.ReadAsStringAsync();
    if (!res.IsSuccessStatusCode) throw new SystemException(response);
    Log.Information("[SEND] {email} {code} {@body}", emails, res.StatusCode, response);
    return res;
  }

  // https://sendgrid.com/docs/for-developers/sending-email/getting-started-email-activity-api/
  public async Task<Response> GetEmailHistory(string email) {
    Dictionary<string, object> data = new Dictionary<string, object> {
      ["limit"] = "10",
      ["query"] = $"to_email%3D%22{HtmlEncoder.Default.Encode(email)}%22"
    };
    string qp = JsonConvert.SerializeObject(data);

    var res = await Client.RequestAsync(BaseClient.Method.GET, queryParams: qp, urlPath: "/messages");
    string response = await res.Body.ReadAsStringAsync();
    Log.Information("[HISTORY] {qp} {code} {@body}", qp, res.StatusCode, response);
    return res;
  }
}

public class SendGridSender<TDb> : SendGridSender where TDb : DbContext, IEmailSenderDb {
  public SendGridSender(IZContext context, IOptions<SendGridOptions> opts) : base(context, opts) {
  }

  // public Task SendRawHtmlAsync(string email, string subject, string message) => ExecuteRawHtml(subject, message, email);

  public override async Task<EmailValidation?> ValidateEmailAsync(string email) {
    var db = Context.GetRequiredService<TDb>();
    Dictionary<string, string> data = new Dictionary<string, string> {
      ["email"] = email
    };
    string body = JsonConvert.SerializeObject(data);

    var res = await Api.RequestAsync(BaseClient.Method.POST, body, urlPath: "/validations/email");
    string response = await res.Body.ReadAsStringAsync();

    try {
      ResultObject<EmailValidation>? validation = JsonConvert.DeserializeObject<ResultObject<EmailValidation>>(response);
      if (validation?.Result == null) throw new FormatException($"Response was not a ValidationResult: {response}");
      var result = validation.Result;
      result.Host ??= "";
      result.Email = email.ToLowerInvariant();
      ZEnv.Log.Information("[VALIDATION] {email} {code} {@result}", email, res.StatusCode, result);

      var cur = await db.EmailValidations.FirstOrDefaultAsync(ev => ev.Email == result.Email);
      if (cur != null) {
        cur.Verdict = result.Verdict;
        cur.Score = result.Score;
        cur.IpAddress = result.IpAddress;
        db.EmailValidations.Update(cur);
      } else {
        await db.EmailValidations.AddAsync(result);
      }
      await db.SaveChangesAsync();

      return validation.Result;
    } catch (Exception e) {
      Log.Warning(e, $"[VALIDATION] Failed to validate email: {email}");
      return null;
    }
  }

  // public Task<Response> SendWelcomeEmail(string email, string playerId, string code) {
  //   return SendTemplate(email, _sendGridOpts.Templates.Welcome, new WelcomeEmailParams() {
  //     PlayerId = playerId,
  //     Code = code,
  //   });
  // }

}

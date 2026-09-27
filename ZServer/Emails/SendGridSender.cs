#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using IZ.Core;
using IZ.Core.Contexts;
using IZ.Core.Json;
using IZ.Core.Observability;
using IZ.Core.Observability.Logging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SendGrid;
using SendGrid.Helpers.Mail;

#endregion

namespace IZ.Server.Emails;

public abstract class SendGridSender : LogicBase {

  public const int MaxHtmlBytes = 80 * 1024;

  /// <summary>RFC 822 headers a caller may attach. Marketing mail uses List-Unsubscribe; everything
  /// else is refused so a template cannot inject arbitrary provider headers.</summary>
  public static readonly HashSet<string> PermittedHeaders = new(StringComparer.OrdinalIgnoreCase) {
    "List-Unsubscribe",
    "List-Unsubscribe-Post",
  };

  /// <summary>The only provider custom-arg key a caller may set. Webhooks bind this opaque id;
  /// they never trust payload email or UserId.</summary>
  public const string CorrelationArg = "nid";

  private readonly SendGridOptions _sendGridOpts;
  private SendGridClient? _api;
  private SendGridClient? _client;
  private EmailAddress? _recipAddress;
  private EmailAddress? _senderEmailAddress;

  protected SendGridSender(IZContext context, IOptions<SendGridOptions> opts) : base(context) {
    _sendGridOpts = opts.Value;
  }

  public EmailAddress SenderEmailAddress => _senderEmailAddress ??=
    string.IsNullOrWhiteSpace(_sendGridOpts.SenderAddress) ? throw new NullReferenceException(nameof(_sendGridOpts.SenderAddress)) :
      new EmailAddress(_sendGridOpts.SenderAddress, _sendGridOpts.SenderName);

  public EmailAddress RecipientEmailAddress => _recipAddress ??=
    string.IsNullOrWhiteSpace(_sendGridOpts.RecipientAddress) ? SenderEmailAddress :
      new EmailAddress(_sendGridOpts.RecipientAddress, _sendGridOpts.RecipientName);

  private string SendGridKey => string.IsNullOrWhiteSpace(_sendGridOpts.Key) ? GetSendGridKeyEnv() : _sendGridOpts.Key;
  protected SendGridClient Client => _client ??= new SendGridClient(SendGridKey);

  /// <summary>Whether signups are address-validated. `SendGrid:ValidatorKey` needs the Email Address Validation
  /// scope, which only some SendGrid plans offer. An empty key means validation is off, not broken: every signup
  /// skips the provider call, and <see cref="LogValidatorState" /> says so once at start-up. A key the provider refuses
  /// (401 revoked, 403 without the scope) is switched off the same way for the rest of the process, after one
  /// <see cref="RefusedKeyLog" /> line (<see cref="NoteProviderAnswer" />). A 5xx or a timeout is not a refusal and is
  /// retried on the next signup.</summary>
  public bool ValidationEnabled => IsValidatorConfigured(_sendGridOpts) && !RefusedKeys.ContainsKey(_sendGridOpts.ValidatorKey);

  public static bool IsValidatorConfigured(SendGridOptions opts) => !string.IsNullOrWhiteSpace(opts.ValidatorKey);

  /// <summary>Keys the provider refused in this process, with the status it answered. Static because the sender is
  /// scoped per request; keyed by the key itself so a rotated key is tried afresh. Never logged.</summary>
  private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, int> RefusedKeys = new();

  public const string RefusedKeyLog = "[VALIDATION] disabled: provider refused the key ({status})";

  /// <summary>401 and 403 are the provider refusing the key itself, never a verdict on one address.</summary>
  public static bool IsKeyRefusal(int status) => status is 401 or 403;

  /// <summary>Records a provider answer. A 401/403 disables validation for this key for the rest of the process and
  /// returns true; only the first refusal of a key logs (<see cref="RefusedKeyLog" />, at Error). Anything else
  /// returns false and changes nothing.</summary>
  protected bool NoteProviderAnswer(int status) {
    if (!IsKeyRefusal(status)) return false;
    if (RefusedKeys.TryAdd(_sendGridOpts.ValidatorKey, status)) Log.Error(RefusedKeyLog, status);
    return true;
  }

  /// <summary>The one start-up line that says whether signups are address-validated. Never prints the key.</summary>
  public static void LogValidatorState(IZLogger log, SendGridOptions opts) {
    if (IsValidatorConfigured(opts)) log.Information("[VALIDATION] enabled");
    else log.Information("[VALIDATION] disabled: SendGrid:ValidatorKey is empty, so signups are not address-validated");
  }

  protected SendGridClient Api => _api ??= new SendGridClient(_sendGridOpts.ValidatorKey);
  private string GetSendGridKeyEnv() {
    string key = $"SENDGRID_API_KEY_{Context.App.ProductName.ToUpperInvariant()}";
    string? env = Environment.GetEnvironmentVariable(key);
    if (string.IsNullOrWhiteSpace(env)) throw new ArgumentException(nameof(SendGridKey));
    return env;
  }

  public abstract Task<EmailValidation?> ValidateEmailAsync(string email);

  /// <summary>One point per call to the provider, tagged `kind` (what the mail is for), `result`
  /// (ok|failed) and `status` (the HTTP status SendGrid answered with). Emitted here because this is
  /// the single place every send passes through, and because a non-2xx used to leave nothing behind
  /// but an exception in a log. The tags are bounded on purpose: never an address, a user or a
  /// message id.</summary>
  public static string SendMetric => $"{ZMetrics.Root}.email.send";

  /// <summary>What a mail is for — the whole vocabulary of the `kind` tag, declared here so it stays
  /// a small closed set rather than growing a value per feature. A caller that does not say gets
  /// <see cref="KindOther" />.</summary>
  public const string KindOther = "other";

  /// <summary>Confirm-your-address mail.</summary>
  public const string KindVerification = "verification";

  /// <summary>Password-reset mail.</summary>
  public const string KindReset = "reset";

  /// <summary>Re-engagement mail a scheduled job decided to send.</summary>
  public const string KindLifecycle = "lifecycle";

  public Task<Response> SendTemplate(string email, string templateId, object args, string kind = KindOther) {
    var msg = new SendGridMessage {
      From = SenderEmailAddress,
      TemplateId = templateId
    };
    msg.SetTemplateData(args);
    return SendOfKind(msg, kind, email);
  }

  /// <summary>Identity's <see cref="Microsoft.AspNetCore.Identity.UI.Services.IEmailSender" /> still
  /// supplies one string. Chordzy templates use <see cref="Send(EmailMessage)" /> so plain text and
  /// HTML stay distinct.</summary>
  public Task<Response> SendRawHtml(string email, string subject, string message, string kind = KindOther) =>
    Send(new EmailMessage {
      To = email,
      Subject = subject,
      Html = message,
      PlainText = message,
      Kind = kind,
    });

  public Task<Response> Send(EmailMessage mail) {
    ArgumentNullException.ThrowIfNull(mail);
    if (string.IsNullOrWhiteSpace(mail.To)) throw new ArgumentException("Email recipient is required");
    return SendOfKind(CreateProviderMessage(mail, SenderEmailAddress), mail.Kind, mail.To);
  }

  public Task<Response> Send(SendGridMessage msg, params string[] emails) => SendOfKind(msg, KindOther, emails);

  /// <summary>Maps the repository-owned <see cref="EmailMessage" /> onto SendGrid without logging
  /// the recipient, the body or the provider payload.</summary>
  public static SendGridMessage CreateProviderMessage(EmailMessage mail, EmailAddress from) {
    ArgumentNullException.ThrowIfNull(mail);
    if (string.IsNullOrWhiteSpace(mail.Subject)) throw new ArgumentException("Email subject is required");
    if (string.IsNullOrWhiteSpace(mail.Html)) throw new ArgumentException("Email HTML is required");
    if (string.IsNullOrWhiteSpace(mail.PlainText)) throw new ArgumentException("Email plain text is required");
    int htmlBytes = Encoding.UTF8.GetByteCount(mail.Html);
    if (htmlBytes > MaxHtmlBytes)
      throw new ArgumentException($"Email HTML is {htmlBytes} bytes; maximum is {MaxHtmlBytes}");

    var msg = new SendGridMessage {
      From = from,
      Subject = mail.Subject,
      PlainTextContent = mail.PlainText,
      HtmlContent = mail.Html,
    };
    ApplyPermittedHeaders(msg, mail.Headers);
    ApplyCorrelation(msg, mail.CorrelationId);
    if (mail.Kind == KindVerification || mail.Kind == KindReset)
      msg.SetOpenTracking(false);
    return msg;
  }

  public static void ApplyCorrelation(SendGridMessage msg, string? correlationId) {
    if (string.IsNullOrWhiteSpace(correlationId)) return;
    if (correlationId.IndexOfAny(['\r', '\n']) >= 0)
      throw new ArgumentException("Email correlation id must be a single line");
    msg.AddGlobalCustomArg(CorrelationArg, correlationId);
  }

  public static void ApplyPermittedHeaders(SendGridMessage msg, IReadOnlyDictionary<string, string>? headers) {
    if (headers == null || headers.Count == 0) return;
    msg.Headers ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    foreach (var pair in headers) {
      if (!PermittedHeaders.Contains(pair.Key))
        throw new ArgumentException("Email header is not permitted");
      if (pair.Value.IndexOfAny(['\r', '\n']) >= 0)
        throw new ArgumentException("Email header value must be a single line");
      msg.Headers[pair.Key] = pair.Value;
    }
  }

  /// <summary>Deliberately not an overload of <see cref="Send" />: `Send(msg, "a@b.com")` would bind
  /// to `kind` and send the mail to nobody.</summary>
  public async Task<Response> SendOfKind(SendGridMessage msg, string kind, params string[] emails) {
    var result = await SendAttempt(msg, kind, emails);
    if (!result.Accepted)
      throw new SystemException($"SendGrid rejected {kind} ({result.StatusCode})");
    return result.Response!;
  }

  /// <summary>
  /// One provider attempt that does not throw on a timeout-after-send. Callers persist Accepted
  /// only when <see cref="EmailSendResult.Accepted" /> is true, Unknown when
  /// <see cref="EmailSendResult.Ambiguous" />, and retry only a conclusive transient rejection.
  /// Never logs the recipient, body, or provider payload.
  /// </summary>
  public async Task<EmailSendResult> SendAttempt(SendGridMessage msg, string kind, params string[] emails) {
    foreach (string email in emails)
      msg.AddTo(new EmailAddress(email));

    // Disable click tracking.
    // See https://sendgrid.com/docs/User_Guide/Settings/tracking.html
    msg.SetClickTracking(false, false);

    Response? res;
    try {
      res = await Client.SendEmailAsync(msg);
    } catch (Exception e) {
      // A send nobody can confirm is a failure until a provider event says otherwise: Error, never a quiet Warning.
      Count(kind, false, 0);
      Log.Error(e, "[SEND] {kind} ambiguous", kind);
      return EmailSendResult.MaybeAccepted();
    }

    int status = (int) res.StatusCode;
    Count(kind, res.IsSuccessStatusCode, status);
    if (res.IsSuccessStatusCode) {
      Log.Information("[SEND] {kind} {code}", kind, res.StatusCode);
      return EmailSendResult.Ok(res, ReadMessageId(res));
    }

    bool transient = status == 429 || status >= 500;
    Log.Error("[SEND] {kind} rejected {code}", kind, status);
    return EmailSendResult.Rejected(status, transient, res);
  }

  public static string? ReadMessageId(Response response) {
    if (response.Headers == null) return null;
    if (!response.Headers.TryGetValues("X-Message-Id", out var values)) return null;
    foreach (string value in values) {
      if (!string.IsNullOrWhiteSpace(value)) return value.Trim();
    }
    return null;
  }

  private void Count(string kind, bool ok, int status) =>
    Context.Metrics?.Increment(SendMetric, tags: new Dictionary<string, object> {
      ["kind"] = kind,
      ["result"] = ok ? "ok" : "failed",
      ["status"] = status,
    });

  /// <summary>
  /// A SendGrid `/validations/email` answer as a verdict, or null with <paramref name="problem" /> saying why: a
  /// non-2xx status with the provider's first error message, or a 2xx body with no `result`. The problem never carries
  /// the body itself, which echoes the address.
  /// </summary>
  public static EmailValidation? ParseValidation(int status, string? body, out string? problem) {
    if (status is < 200 or >= 300) {
      problem = $"HTTP {status}: {ProviderErrorMessage(body) ?? "no error message"}";
      return null;
    }
    ResultObject<EmailValidation>? parsed = null;
    try {
      parsed = string.IsNullOrWhiteSpace(body) ? null : ZJson.DeserializeObject<ResultObject<EmailValidation>>(body);
    } catch (Exception e) {
      problem = $"HTTP {status}: unreadable body ({e.GetType().Name})";
      return null;
    }
    if (parsed?.Result == null || string.IsNullOrWhiteSpace(parsed.Result.Verdict)) {
      problem = $"HTTP {status}: no result in the body";
      return null;
    }
    problem = null;
    return parsed.Result;
  }

  /// <summary>SendGrid's `{"errors":[{"message":"…"}]}`, first message only, bounded, with anything address-shaped
  /// removed.</summary>
  public static string? ProviderErrorMessage(string? body) {
    if (string.IsNullOrWhiteSpace(body)) return null;
    try {
      string? message = ZJson.DeserializeObject<ProviderErrorBody>(body)?.Errors?.FirstOrDefault(e => !string.IsNullOrWhiteSpace(e.Message))?.Message;
      if (message == null) return null;
      message = string.Join(' ', message.Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(w => !w.Contains('@')));
      return message.Length > 200 ? message[..200] : message;
    } catch (Exception) {
      return null;
    }
  }

  // https://sendgrid.com/docs/for-developers/sending-email/getting-started-email-activity-api/
  public async Task<Response> GetEmailHistory(string email) {
    var data = new Dictionary<string, string> {
      ["limit"] = "10",
      ["query"] = "to_email%3D%22" + Uri.EscapeDataString(email) + "%22"
    };
    string qp = ZJson.SerializeObject(data);

    var res = await Client.RequestAsync(BaseClient.Method.GET, queryParams: qp, urlPath: "/messages");
    Log.Information("[HISTORY] {code}", res.StatusCode);
    return res;
  }
}

public class SendGridSender<TDb> : SendGridSender where TDb : DbContext, IEmailSenderDb {
  public SendGridSender(IZContext context, IOptions<SendGridOptions> opts) : base(context, opts) { }

  /// <summary>The one provider call: the HTTP status and body of `POST /v3/validations/email`. Virtual so a test can
  /// answer without the network.</summary>
  protected virtual async Task<(int Status, string Body)> RequestValidationAsync(string body) {
    var res = await Api.RequestAsync(BaseClient.Method.POST, body, urlPath: "/validations/email");
    return ((int) res.StatusCode, await res.Body.ReadAsStringAsync());
  }

  /// <summary>
  /// Null without a call or a log when validation is off (<see cref="SendGridSender.ValidationEnabled" />): no key, or a
  /// key the provider already refused. The refusing answer itself logs once and returns null (staging 1559 logged
  /// `validator gave no verdict: HTTP 401` on every signup).
  /// The provider's verdict on an address, or null when the validator gave none. A null never refuses a signup (the
  /// caller fails open), so every null is logged at Error with the reason a person can act on — the HTTP status and
  /// SendGrid's own message (a key without the Email Address Validation scope answers 403 "access forbidden"),
  /// never the address. Production logged only "Response was not a ValidationResult" from 2026-09-17 to 2026-09-26.
  /// </summary>
  public override async Task<EmailValidation?> ValidateEmailAsync(string email) {
    if (!ValidationEnabled) return null;
    int status;
    string response;
    try {
      string body = ZJson.SerializeObject(new EmailValidationRequest { Email = email });
      (status, response) = await RequestValidationAsync(body);
    } catch (Exception e) {
      Log.Error(e, "[VALIDATION] validator unavailable: {reason}", e.Message);
      return null;
    }
    if (NoteProviderAnswer(status)) return null;

    var result = ParseValidation(status, response, out string? problem);
    if (result == null) {
      Log.Error("[VALIDATION] validator gave no verdict: {reason}", problem);
      return null;
    }
    try {
      var db = Context.GetRequiredService<TDb>();
      result.Host ??= "";
      result.Email = email.ToLowerInvariant();
      Log.Information("[VALIDATION] {code} verdict {verdict}", status, result.Verdict);

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
    } catch (Exception e) {
      // The verdict stands; only its cache row failed.
      Log.Error(e, "[VALIDATION] could not store the verdict");
    }
    return result;
  }
}

internal sealed class EmailValidationRequest {
  public string Email { get; set; } = null!;
}

public sealed class ProviderErrorBody {
  public List<ProviderError>? Errors { get; set; }
}

public sealed class ProviderError {
  public string? Message { get; set; }
}

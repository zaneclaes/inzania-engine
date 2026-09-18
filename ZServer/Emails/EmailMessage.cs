#region

using System.Collections.Generic;

#endregion

namespace IZ.Server.Emails;

/// <summary>
/// One outbound mail as Chordzy (and any other IZ host) hands it to
/// <see cref="SendGridSender" />: distinct subject, preview, plain text and HTML, plus the small
/// set of headers the provider is allowed to emit. Templates stay in the consuming repository;
/// this type is the wire contract, not a second SendGrid template universe.
/// </summary>
public sealed class EmailMessage {
  public string To { get; init; } = null!;

  public string Subject { get; init; } = null!;

  /// <summary>Inbox preheader. The Chordzy renderer also inlines it hidden at the top of
  /// <see cref="Html" />; the sender keeps the field so a caller never has to stuff preview text
  /// into the subject or the plain-text body.</summary>
  public string? Preview { get; init; }

  public string PlainText { get; init; } = null!;

  public string Html { get; init; } = null!;

  /// <summary>Optional extra RFC 822 headers. Keys must be in
  /// <see cref="SendGridSender.PermittedHeaders" /> and values must be single-line.</summary>
  public IReadOnlyDictionary<string, string>? Headers { get; init; }

  public string Kind { get; init; } = SendGridSender.KindOther;
}

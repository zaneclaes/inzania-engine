#region

using System;
using System.Text.RegularExpressions;
using IZ.Core.Utils;

#endregion

namespace IZ.Core.Observability.Logging;

/// <summary>
/// Classifies a thrown exception for the existing trace path.
/// First-chance counts are a CLR counter, not a request failure.
/// A caught exception stays off the span's error flag.
/// A failed boundary keeps error type, message and stack.
/// </summary>
public static class RuntimeExceptionReport {
  public const string FirstChanceKind = "first-chance";
  public const string CaughtKind = "caught";
  public const string FailedKind = "failed";

  public static bool MarksSpanFailed(string kind) => kind == FailedKind;

  public static string TypeOf(Exception ex) => ex.GetType().FullName ?? ex.GetType().Name;

  public static string SafeText(string? value) {
    if (string.IsNullOrEmpty(value)) return "";
    string text = Bearer.Replace(value, "$1 [redacted]");
    text = SecretAssign.Replace(text, "$1=[redacted]");
    text = IdentityAssign.Replace(text, "$1=[redacted]");
    text = Email.Replace(text, "[redacted-email]");
    return text;
  }

  /// <summary>True when a tag value would name a person, credential, or one request.</summary>
  public static bool IdentifiesUserOrRequest(string? value) {
    if (string.IsNullOrEmpty(value)) return false;
    if (value.IndexOf('@') >= 0) return true;
    return IdentityWord.IsMatch(value);
  }

  public static string StackOf(Exception ex) {
    string raw = ex.ToString();
    var frames = StackTraces.Filter(raw);
    string text = frames.Count > 0 ? string.Join("\n", frames) : raw;
    text = SafeText(text);
    return text.Length <= 4000 ? text : text.Substring(0, 4000);
  }

  public static string SourceOf(Exception ex, string? source) {
    if (!string.IsNullOrWhiteSpace(source) && !IdentifiesUserOrRequest(source))
      return Trim(SafeText(source));
    var frames = StackTraces.Filter(ex.ToString());
    string frame = frames.Count > 0 ? frames[0] : ex.GetType().Name;
    return Trim(SafeText(frame));
  }

  private static string Trim(string text) => text.Length <= 300 ? text : text.Substring(0, 300);

  private static readonly Regex Bearer = new Regex(
    @"(?i)\b(bearer|basic)\s+[A-Za-z0-9._~+/=-]{6,}", RegexOptions.Compiled);

  private static readonly Regex SecretAssign = new Regex(
    "(?i)(password|passwd|token|secret|api[_-]?key|authorization|access[_-]?key)\"?\\s*[:=]\\s*(?:\"[^\"]*\"|\\S+)",
    RegexOptions.Compiled);

  private static readonly Regex IdentityAssign = new Regex(
    "(?i)(user(?:_?id)?|request(?:_?id)?|email)\"?\\s*[:=]\\s*(?:\"[^\"]*\"|\\S+)",
    RegexOptions.Compiled);

  private static readonly Regex Email = new Regex(
    @"[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}", RegexOptions.Compiled);

  private static readonly Regex IdentityWord = new Regex(
    @"(?i)(^|[^a-z])(user(_?id)?|request(_?id)?|email|bearer|password|token|secret)([^a-z]|$)",
    RegexOptions.Compiled);
}

#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

#endregion

namespace IZ.Core.Utils;

public static class StringUtils {
  private static readonly Regex regexAlphaNumeric = new Regex("[^a-zA-Z0-9 ]");

  public static string UrlToShopId(this string url) => url.Split("//").Last().ToSnakeCase();

  public static string Slugify(this string name) => regexAlphaNumeric.Replace( // lowercase string separated by dashes (strips out punctuation)
    name.ToSnakeCase().Replace('_', ' '), string.Empty
  ).Trim().Replace(' ', '-');

  public static string ToFieldName(this string str) => str.ToCamelCase();

  public static string ToCamelCase(this string snakeCase) {
    List<string>? parts = snakeCase.Split('_').Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
    if (!parts.Any()) return "";
    string? firstWord = parts[0];
    parts.RemoveAt(0);

    return firstWord.Substring(0, 1).ToLowerInvariant() + firstWord.Substring(1)
                                                        + string.Join("", parts.Select(s => char.ToUpperInvariant(s[0]) + s.Substring(1)));
  }

  public static bool IsNumeric(this string input) {
    if (input.StartsWith("-")) input = input.Substring(1);
    string[] parts = input.Split('.');
    if (parts.Length > 2) return false;
    return parts.All(p => p.ToCharArray().All(char.IsDigit));
  }

  public static string ToTitleCase(this string camelCase) => camelCase.ToTitleCase("");

  public static string ToTitleWords(this string camelCase) => camelCase.ToTitleCase(" ");

  public static string ToTitleCase<T>(this T e) where T : Enum => e.ToString().ToTitleCase();
  public static string ToTitleWords<T>(this T e) where T : Enum => e.ToString().ToTitleWords();
  public static string ToProperTitle<T>(this T e) where T : Enum => e.ToString().ToTitleWords().ToProperTitle();

  private static string ToTitleCase(this string camelCase, string join) => string.Join(join, camelCase
      .ToAlphaNumericChunks()
      .Select(s => s.Substring(0, 1).ToUpper() + (s.Length > 1 ? s.Substring(1) : "")));

  private static readonly HashSet<string> _smallWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
    "a", "an", "and", "as", "at", "but", "by",
    "for", "if", "in", "nor", "of", "on", "or",
    "per", "the", "to", "vs", "via"
  };

  public static string ToProperTitle(this string input) {
    var words = Regex.Split(input.ToLowerInvariant().Trim(), @"(\s-_)+")
      .Where(s => !string.IsNullOrWhiteSpace(s)).ToArray();
    for (int i = 0; i < words.Length; i++) {
      string word = words[i];
      if (word.Length == 0) continue;

      // Capitalize if not a small word or if it's first/last
      if (i == 0 || i == words.Length - 1 || !_smallWords.Contains(word)) {
        // Preserve punctuation like “(foo)” or “foo:bar”
        var match = Regex.Match(word, @"^(\W*)(\w)(\w*)(\W*)$");
        if (match.Success) {
          words[i] = match.Groups[1].Value +
                     char.ToUpper(match.Groups[2].Value[0]) +
                     match.Groups[3].Value +
                     match.Groups[4].Value;
        } else {
          words[i] = char.ToUpper(word[0]) + (word.Length > 1 ? word.Substring(1) : "");
        }
      }
    }

    return string.Join(" ", words);
  }

  public static string ToKebabCase(this Enum camelCase, bool? lower = true) => camelCase.ToString().ToSnakeCase(lower, '-');

  public static string ToKebabCase(this string camelCase, bool? lower = true) => camelCase.ToSnakeCase(lower, '-');

  private static readonly HashSet<char> _nonSplitChars = new HashSet<char>() {'\'', '’', '"', '.'};

  public static string[] ToAlphaNumericChunks(this string str, bool separateOnCase = true, bool separateNumbers = false) {
    List<string> chunks = new List<string>();
    bool pendingSeparator = false;
    var sb = new StringBuilder();
    bool numeric = false;
    for (int i = 0; i < str.Length; ++i) {
      char c = str[i];
      if (!char.IsLetter(c) && !char.IsNumber(c)) {
        if (!_nonSplitChars.Contains(c)) pendingSeparator = true;
        continue;
      }
      if (separateNumbers) {
        if (numeric && !char.IsNumber(c)) {
          pendingSeparator = true;
        } else if (!numeric && !char.IsLetter(c)) {
          pendingSeparator = true;
        }
        numeric = char.IsNumber(c);
      }
      if (separateOnCase && char.IsUpper(c)) {
        pendingSeparator = true;
      }
      if (pendingSeparator && sb.Length > 0) {
        chunks.Add(sb.ToString());
        sb.Clear();
      }
      sb.Append(c);
      pendingSeparator = false;
    }
    if (sb.Length > 0) chunks.Add(sb.ToString());
    return chunks.ToArray();
  }

  public static string ToSnakeCase(this string camelCase, bool? lower = true, char separator = '_') {
    if (camelCase == null) throw new ArgumentNullException(nameof(camelCase));
    string s = string.Join(separator, camelCase.ToAlphaNumericChunks());
    if (lower == null) return s;
    return lower.Value! ? s.ToLowerInvariant() : s.ToUpperInvariant();
  }

  public static string SubstringFromLastUppercase(this string target) {

    int lastUppercaseIndex = Array.FindLastIndex(target.ToCharArray(), char.IsUpper);
    return lastUppercaseIndex >= 0 ? target.Substring(lastUppercaseIndex) : target;
  }

  public static string[] SplitByCapital(this string target) => Regex.Replace(target, "((?<=[a-z])[A-Z]|[A-Z](?=[a-z]))", " $1").Trim().Split(' ');
}

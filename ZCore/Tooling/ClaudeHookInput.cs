#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using IZ.Core.Json;

#endregion

namespace IZ.Core.Tooling;

/// <summary>
/// The payload Claude Code writes to a hook's stdin (`PreToolUse` / `PostToolUse`), read with `ZJson` like any other
/// JSON. The engine's guards (`.claude/hooks/*.cs`) all start from it, so the shape and the "what will the file
/// read like after this edit" arithmetic live here once rather than in every hook.
/// </summary>
public class ClaudeHookInput {
  [JsonPropertyName("tool_name")] public string ToolName { get; set; } = "";

  [JsonPropertyName("tool_input")] public ClaudeToolInput? ToolInput { get; set; }

  /// <summary>The hook's stdin, or null when it is empty or not a hook payload (a hook then allows the call).</summary>
  public static ClaudeHookInput? Read(string stdin) {
    if (string.IsNullOrWhiteSpace(stdin)) return null;
    try {
      return ZJson.DeserializeObject<ClaudeHookInput>(null, stdin);
    } catch (Exception) {
      return null;
    }
  }
}

public class ClaudeToolInput {
  [JsonPropertyName("file_path")] public string? FilePath { get; set; }

  /// <summary>`Write`: the whole new file.</summary>
  [JsonPropertyName("content")] public string? Content { get; set; }

  [JsonPropertyName("old_string")] public string? OldString { get; set; }

  [JsonPropertyName("new_string")] public string? NewString { get; set; }

  [JsonPropertyName("replace_all")] public bool ReplaceAll { get; set; }

  /// <summary>`MultiEdit`: applied in order.</summary>
  [JsonPropertyName("edits")] public List<ClaudeEdit>? Edits { get; set; }

  /// <summary>`Bash`: the command line.</summary>
  [JsonPropertyName("command")] public string? Command { get; set; }

  /// <summary>Every piece of text the call writes: the whole file for `Write`, each replacement for `Edit` / `MultiEdit`.</summary>
  public string NewText => Content ?? NewString ?? string.Join("\n", (Edits ?? new List<ClaudeEdit>()).Select(e => e.NewString ?? ""));

  /// <summary>Every piece of text the call replaces (empty for `Write`).</summary>
  public string OldText => Content != null ? "" : OldString ?? string.Join("\n", (Edits ?? new List<ClaudeEdit>()).Select(e => e.OldString ?? ""));

  /// <summary>True when the call writes file text at all (`Write`, `Edit` or `MultiEdit`).</summary>
  public bool WritesText => Content != null || NewString != null || Edits is { Count: > 0 };

  /// <summary>
  /// The file as it will read after the call, given what is on disk now. A stale edit (its `old_string` no longer in
  /// the file) appends its replacement, so a guard still judges the text being written.
  /// </summary>
  public string ProspectiveContent(string existing) {
    if (Content != null) return Content;
    if (NewString != null) return ClaudeEdit.Apply(existing, OldString, NewString, ReplaceAll);
    return (Edits ?? new List<ClaudeEdit>()).Aggregate(existing, (text, e) => ClaudeEdit.Apply(text, e.OldString, e.NewString ?? "", e.ReplaceAll));
  }
}

public class ClaudeEdit {
  [JsonPropertyName("old_string")] public string? OldString { get; set; }

  [JsonPropertyName("new_string")] public string? NewString { get; set; }

  [JsonPropertyName("replace_all")] public bool ReplaceAll { get; set; }

  public static string Apply(string text, string? oldS, string newS, bool all) {
    if (string.IsNullOrEmpty(oldS)) return text + "\n" + newS;
    int idx = text.IndexOf(oldS, StringComparison.Ordinal);
    if (idx < 0) return text + "\n" + newS;
    return all ? text.Replace(oldS, newS) : text.Substring(0, idx) + newS + text.Substring(idx + oldS.Length);
  }
}

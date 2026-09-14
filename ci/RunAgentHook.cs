#!/usr/bin/env dotnet
#:project ../ZCore/ZCore.csproj
// Adapter shared by Claude Code and Codex hook declarations. Claude already sends the shape the
// guards consume. Codex sends `apply_patch` as one unified diff, so this splits it into one
// Claude-compatible edit payload per file before running the guard. Guard logic stays in one file.
using System.Diagnostics;
using System.Text.Json.Serialization;
using IZ.Core.Contexts;
using IZ.Core.Json;

ZScriptApp.Start("RunAgentHook");
if (args.Length < 2 || args[0] != "--guard") {
  Console.Error.WriteLine("Usage: dotnet run RunAgentHook.cs -- --guard <guard.cs> [guard arguments]");
  return 1;
}

string guard = args[1];
string[] guardArgs = args.Skip(2).ToArray();
string stdin = Console.In.ReadToEnd();
foreach (string payload in Payloads(stdin)) {
  var result = RunGuard(guard, guardArgs, payload);
  if (result.Output.Length > 0) Console.Error.Write(result.Output);
  if (result.Code != 0) return result.Code;
}
return 0;

static IEnumerable<string> Payloads(string stdin) {
  HookEnvelope? hook = null;
  bool invalid = false;
  try {
    hook = ZJson.DeserializeObject<HookEnvelope>(null, stdin);
  } catch {
    invalid = true;
  }
  if (invalid) {
    yield return stdin;
    yield break;
  }
  if (hook?.ToolName != "apply_patch" || string.IsNullOrWhiteSpace(hook.ToolInput?.Command)) {
    yield return stdin;
    yield break;
  }

  foreach (PatchFile patch in ParsePatch(hook.ToolInput.Command)) {
    var converted = new HookEnvelope {
      ToolName = "Write",
      ToolInput = new HookToolInput {
        FilePath = patch.Path,
        NewString = patch.Added,
        OldString = patch.Removed,
      },
    };
    yield return ZJson.SerializeObject(converted);
  }
}

static IEnumerable<PatchFile> ParsePatch(string patch) {
  string? path = null;
  var added = new List<string>();
  var removed = new List<string>();
  foreach (string line in patch.Replace("\r\n", "\n").Split('\n')) {
    const string add = "*** Add File: ";
    const string update = "*** Update File: ";
    const string delete = "*** Delete File: ";
    if (line.StartsWith(add, StringComparison.Ordinal) || line.StartsWith(update, StringComparison.Ordinal) || line.StartsWith(delete, StringComparison.Ordinal)) {
      if (path != null) yield return new PatchFile(path, string.Join("\n", added), string.Join("\n", removed));
      path = line.Substring(line.IndexOf(": ", StringComparison.Ordinal) + 2).Trim();
      added.Clear();
      removed.Clear();
      continue;
    }
    if (path == null) continue;
    if (line.StartsWith('+') && !line.StartsWith("+++", StringComparison.Ordinal)) added.Add(line.Substring(1));
    if (line.StartsWith('-') && !line.StartsWith("---", StringComparison.Ordinal)) removed.Add(line.Substring(1));
  }
  if (path != null) yield return new PatchFile(path, string.Join("\n", added), string.Join("\n", removed));
}

static (int Code, string Output) RunGuard(string guard, IEnumerable<string> guardArgs, string input) {
  bool csharp = Path.GetExtension(guard).Equals(".cs", StringComparison.OrdinalIgnoreCase);
  var start = new ProcessStartInfo(csharp ? "dotnet" : "sh") {
    RedirectStandardInput = true,
    RedirectStandardOutput = true,
    RedirectStandardError = true,
  };
  if (csharp) start.ArgumentList.Add("run");
  start.ArgumentList.Add(guard);
  start.ArgumentList.Add("--");
  foreach (string arg in guardArgs) start.ArgumentList.Add(arg);
  string root = GitRoot();
  start.Environment["CLAUDE_PROJECT_DIR"] = root;
  start.Environment["CODEX_PROJECT_DIR"] = root;
  using Process process = Process.Start(start)!;
  process.StandardInput.Write(input);
  process.StandardInput.Close();
  string output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
  process.WaitForExit();
  return (process.ExitCode, output);
}

static string GitRoot() {
  var start = new ProcessStartInfo("git") { RedirectStandardOutput = true };
  start.ArgumentList.Add("rev-parse");
  start.ArgumentList.Add("--show-toplevel");
  using Process process = Process.Start(start)!;
  string root = process.StandardOutput.ReadToEnd().Trim();
  process.WaitForExit();
  return root.Length > 0 ? root : Directory.GetCurrentDirectory();
}

class HookEnvelope {
  [JsonPropertyName("tool_name")] public string? ToolName { get; set; }
  [JsonPropertyName("tool_input")] public HookToolInput? ToolInput { get; set; }
}

class HookToolInput {
  [JsonPropertyName("command")] public string? Command { get; set; }
  [JsonPropertyName("file_path")] public string? FilePath { get; set; }
  [JsonPropertyName("old_string")] public string? OldString { get; set; }
  [JsonPropertyName("new_string")] public string? NewString { get; set; }
}

record PatchFile(string Path, string Added, string Removed);

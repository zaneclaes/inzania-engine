#!/usr/bin/env dotnet
// inzania-engine install — wires a consuming repo up to the engine's tooling. Run it after cloning,
// after pulling a submodule bump, or any time the engine's hook set changes:
//
//     dotnet run inzania-engine/ci/install.cs          # install / update
//     dotnet run inzania-engine/ci/install.cs -- --check  # report drift, change nothing (exit 1 if drifted)
//
// It is idempotent: running it twice changes nothing the second time, and running it after the engine
// gains, loses or edits a hook converges the repo onto the new set. That is the whole point — the repos
// that vendor the engine should pick up a new guard by re-running one command, not by hand-editing
// their own settings.json and drifting apart.
//
// TWO INSTALLERS IN ONE, because the two hook systems are complementary and forgetting either one is
// silent:
//
//  1. GIT HOOKS — symlinks the repo's own `ci/hooks/*` scripts into `.git/hooks/`. Symlinks rather
//     than core.hooksPath: git-lfs owns .git/hooks/{post-checkout,post-commit,post-merge,pre-push} and
//     redirecting the path would disable them with no error. Where a repo hook takes over one of those
//     four it must chain to lfs itself, so this refuses to install one that does not — a silent stop to
//     large-file uploads is worse than a failed install.
//
//  2. CLAUDE HOOKS — merges the engine's `ci/claude-hooks.json` into the repo's `.claude/settings.json`.
//     Engine-owned entries are recognized by their command pointing into the engine's `.claude/hooks/`
//     folder, so the merge is a clean replace of exactly those: a hook the engine dropped disappears, a
//     changed command or timeout is rewritten, and everything the repo declares for itself is untouched.
//     No marker keys are written into settings.json, so nothing here depends on Claude Code tolerating
//     unknown fields.
//
// Exit 0 = installed (or already current), 1 = failed, or under --check, drift found.
using System.Diagnostics;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

bool check = Args().Contains("--check");
var changes = new List<string>();

string root = Run("git", ["rev-parse", "--show-toplevel"]).Output.Trim();
if (root.Length <= 0) {
  Console.Error.WriteLine("[install] not inside a git repository.");
  return 1;
}

// This file lives at <engine>/ci/install.cs, so the engine is one directory above `ci`. Resolved from
// the script's own location rather than a hardcoded "inzania-engine", so a repo that vendors it under a
// different name works with no configuration. AppContext.BaseDirectory is no use here — a file-based
// app runs out of a build cache, nowhere near its source — so the path comes from [CallerFilePath],
// with a search from the repo root as the fallback for a repo moved since that path was baked in.
string engine = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(ScriptPath()) ?? ".", ".."));
if (!File.Exists(Path.Combine(engine, "ci", "claude-hooks.json"))) {
  engine = Directory.EnumerateFiles(root, "claude-hooks.json", SearchOption.AllDirectories)
             .Where(f => Path.GetFileName(Path.GetDirectoryName(f)) == "ci")
             .Select(f => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(f)!, "..")))
             .FirstOrDefault()
           ?? root;
}
string engineRel = Path.GetRelativePath(root, engine).Replace('\\', '/');
bool engineIsRoot = engineRel is "." or "";

Console.WriteLine($"[install] repo {root}");
Console.WriteLine($"[install] engine {(engineIsRoot ? "(this repo)" : engineRel)}");

if (!InstallGitHooks()) return 1;
if (!InstallClaudeHooks()) return 1;

if (changes.Count <= 0) {
  Console.WriteLine("[install] everything already current.");
  return 0;
}
if (check) {
  Console.Error.WriteLine($"[install] {changes.Count} item(s) out of date — run without --check to fix:");
  foreach (string c in changes) Console.Error.WriteLine($"  {c}");
  return 1;
}
Console.WriteLine($"[install] {changes.Count} item(s) updated.");
return 0;

// ---------------------------------------------------------------------------------------------
// 1. Git hooks
// ---------------------------------------------------------------------------------------------
bool InstallGitHooks() {
  string src = Path.Combine(root, "ci", "hooks");
  string dst = Path.Combine(root, ".git", "hooks");
  if (!Directory.Exists(src)) {
    Console.WriteLine("[install] no ci/hooks/ in this repo; no git hooks to install.");
    return true;
  }
  if (!Directory.Exists(dst)) {
    // A submodule or worktree keeps its git dir elsewhere; ask git rather than assuming .git is one.
    string gitDir = Run("git", ["rev-parse", "--git-dir"]).Output.Trim();
    dst = Path.Combine(Path.IsPathRooted(gitDir) ? gitDir : Path.Combine(root, gitDir), "hooks");
    Directory.CreateDirectory(dst);
  }

  foreach (string hook in Directory.GetFiles(src).OrderBy(f => f)) {
    string name = Path.GetFileName(hook);
    // The .cs files in ci/hooks/ are the reusable checks the shell hooks call, not hooks themselves —
    // git would try to execute them by their extensionless name and fail.
    if (name.EndsWith(".cs") || name.StartsWith('.')) continue;

    string target = Path.Combine(dst, name);
    var info = new FileInfo(target);
    string? current = info.LinkTarget;   // null when the path is absent or a real file rather than a link.

    if (info.Exists && current == null && File.ReadAllText(target).Contains("git lfs")
        && !File.ReadAllText(hook).Contains("git lfs")) {
      Console.Error.WriteLine($"[install] REFUSING {name}: it is git-lfs's hook and ci/hooks/{name} does not chain to it.");
      Console.Error.WriteLine($"[install] add `git lfs {name} \"$@\"` to ci/hooks/{name}, then re-run.");
      return false;
    }

    // Relative link, so the repo can be moved or cloned to a different path and keep working.
    string link = Path.Combine("..", "..", "ci", "hooks", name);
    if (current == link) continue;

    changes.Add($"git hook {name}");
    if (check) continue;
    if (info.Exists || current != null) File.Delete(target);
    File.CreateSymbolicLink(target, link);
    Console.WriteLine($"[install] git hook {name}");
  }
  return true;
}

// ---------------------------------------------------------------------------------------------
// 2. Claude hooks
// ---------------------------------------------------------------------------------------------
bool InstallClaudeHooks() {
  string manifestPath = Path.Combine(engine, "ci", "claude-hooks.json");
  if (!File.Exists(manifestPath)) {
    Console.WriteLine("[install] no ci/claude-hooks.json in the engine; no Claude hooks to install.");
    return true;
  }

  JsonNode? manifest;
  try {
    manifest = JsonNode.Parse(File.ReadAllText(manifestPath), null, new JsonDocumentOptions {
      CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true,
    });
  } catch (JsonException e) {
    Console.Error.WriteLine($"[install] {engineRel}/ci/claude-hooks.json is not valid JSON: {e.Message}");
    return false;
  }

  var declared = manifest?["hooks"]?.AsArray();
  if (declared == null) {
    Console.Error.WriteLine($"[install] {engineRel}/ci/claude-hooks.json has no \"hooks\" array.");
    return false;
  }

  // The marker that says "the engine put this here". Path-based, so no bookkeeping field has to survive
  // in settings.json and a repo-declared hook can never be mistaken for one of ours.
  string owned = (engineIsRoot ? "" : engineRel + "/") + ".claude/hooks/";

  string settingsPath = Path.Combine(root, ".claude", "settings.json");
  JsonObject settings;
  string before = File.Exists(settingsPath) ? File.ReadAllText(settingsPath) : "";
  if (before.Trim().Length > 0) {
    try {
      settings = JsonNode.Parse(before, null, new JsonDocumentOptions {
        CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true,
      })!.AsObject();
    } catch (JsonException e) {
      Console.Error.WriteLine($"[install] .claude/settings.json is not valid JSON: {e.Message}");
      return false;
    }
  } else {
    settings = new JsonObject();
  }

  if (settings["hooks"] is not JsonObject events) settings["hooks"] = events = new JsonObject();

  // Strip every engine-owned entry first, then re-add from the manifest: one pass handles additions,
  // command/timeout edits, matcher moves and deletions alike, and converges however far behind the repo was.
  foreach (string evt in events.Select(p => p.Key).ToList()) {
    if (events[evt] is not JsonArray groups) continue;
    for (int g = groups.Count - 1; g >= 0; g--) {
      if (groups[g] is not JsonObject group || group["hooks"] is not JsonArray entries) continue;
      for (int h = entries.Count - 1; h >= 0; h--)
        if (entries[h]?["command"]?.GetValue<string>()?.Contains(owned) == true)
          entries.RemoveAt(h);
      if (entries.Count <= 0) groups.RemoveAt(g);   // a group that only ever held engine hooks.
    }
    if (groups.Count <= 0) events.Remove(evt);
  }

  foreach (var entry in declared) {
    string evt = entry?["event"]?.GetValue<string>() ?? "";
    string matcher = entry?["matcher"]?.GetValue<string>() ?? "";
    string command = (entry?["command"]?.GetValue<string>() ?? "").Replace("$ENGINE/", engineIsRoot ? "" : engineRel + "/");
    if (evt.Length <= 0 || command.Length <= 0) {
      Console.Error.WriteLine("[install] claude-hooks.json: an entry is missing \"event\" or \"command\".");
      return false;
    }

    if (events[evt] is not JsonArray groups) events[evt] = groups = new JsonArray();
    var group = groups.FirstOrDefault(g => (g?["matcher"]?.GetValue<string>() ?? "") == matcher) as JsonObject;
    if (group == null) {
      group = new JsonObject { ["matcher"] = matcher, ["hooks"] = new JsonArray() };
      groups.Add((JsonNode) group);   // the generic Add<T> is the trim/AOT-unsafe one.
    }
    if (group["hooks"] is not JsonArray entries) group["hooks"] = entries = new JsonArray();

    var node = new JsonObject { ["type"] = "command", ["command"] = command };
    if (entry?["timeout"] is { } t) node["timeout"] = t.DeepClone();
    // Engine guards run first within their group: they are the shared invariants, and a repo-specific
    // hook is cheaper to reach after the general ones have already rejected an edit.
    entries.Insert(CountEngineEntries(entries, owned), node);
  }

  string after = settings.ToJsonString(JsonOut()) + "\n";
  if (Normalize(before) == Normalize(after)) {
    Console.WriteLine($"[install] claude hooks current ({declared.Count} from the engine).");
    return true;
  }

  changes.Add($".claude/settings.json ({declared.Count} engine hook(s))");
  if (check) return true;
  Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!);
  File.WriteAllText(settingsPath, after);
  Console.WriteLine($"[install] claude hooks written to .claude/settings.json ({declared.Count} from the engine).");
  return true;
}

static int CountEngineEntries(JsonArray entries, string owned) =>
  entries.Count(e => e?["command"]?.GetValue<string>()?.Contains(owned) == true);

static string Normalize(string json) {
  if (json.Trim().Length <= 0) return "";
  try {
    return JsonNode.Parse(json, null, new JsonDocumentOptions {
      CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true,
    })!.ToJsonString(JsonOut());
  } catch (JsonException) {
    return json;
  }
}

static string ScriptPath([System.Runtime.CompilerServices.CallerFilePath] string path = "") => path;

static string[] Args() => Environment.GetCommandLineArgs().Skip(1).ToArray();

static (int Code, string Output) Run(string file, IEnumerable<string> args) {
  var psi = new ProcessStartInfo(file) { RedirectStandardOutput = true, RedirectStandardError = true };
  foreach (string a in args) psi.ArgumentList.Add(a);
  using var p = Process.Start(psi)!;
  var stdout = p.StandardOutput.ReadToEndAsync();
  var stderr = p.StandardError.ReadToEndAsync();
  p.WaitForExit();
  return (p.ExitCode, stdout.Result + stderr.Result);
}

// UnsafeRelaxedJsonEscaping so the quotes inside a hook command stay `\"` rather than becoming
// `"`. Both are valid JSON and Claude Code reads either, but the escaped form churns the whole
// file on first write and is unreadable in review. "Unsafe" refers to HTML-embedding, not to files.
static JsonSerializerOptions JsonOut() => new() { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

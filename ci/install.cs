#!/usr/bin/env dotnet
#:project ../ZCore/ZCore.csproj
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
// THREE STEPS, because the hook systems are complementary and forgetting either one is silent:
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
//  3. PRE-BUILD — every engine hook is a file-based script referencing ZCore (`#:project`), and several run
//     at once on each edit. Cold, they would all build ZCore at the same moment, and concurrent builds of one
//     project fail at random. Building them one at a time here means the hooks start warm.
//
// JSON goes through ZJson like everywhere else (`.claude/hooks/JsonGuard.cs`): the manifest into a typed model,
// settings.json as plain dictionaries so keys this file knows nothing about survive untouched.
// Exit 0 = installed (or already current), 1 = failed, or under --check, drift found.
using System.Diagnostics;
using System.Text.RegularExpressions;
using IZ.Core.Contexts;
using IZ.Core.Json;

ZScriptApp.Start("install");
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
if (!InstallClaudeHooks(out var hookScripts)) return 1;
if (!check) PrebuildHookScripts(hookScripts);

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
bool InstallClaudeHooks(out List<string> scripts) {
  scripts = new List<string>();
  string manifestPath = Path.Combine(engine, "ci", "claude-hooks.json");
  if (!File.Exists(manifestPath)) {
    Console.WriteLine("[install] no ci/claude-hooks.json in the engine; no Claude hooks to install.");
    return true;
  }

  HookManifest? manifest;
  try {
    manifest = ZJson.DeserializeObject<HookManifest>(null, File.ReadAllText(manifestPath), Lenient());
  } catch (Exception e) {
    Console.Error.WriteLine($"[install] {engineRel}/ci/claude-hooks.json is not valid JSON: {e.Message}");
    return false;
  }
  var declared = manifest?.Hooks;
  if (declared == null) {
    Console.Error.WriteLine($"[install] {engineRel}/ci/claude-hooks.json has no hooks array.");
    return false;
  }

  // The marker that says "the engine put this here". Path-based, so no bookkeeping field has to survive
  // in settings.json and a repo-declared hook can never be mistaken for one of ours.
  string owned = (engineIsRoot ? "" : engineRel + "/") + ".claude/hooks/";

  string settingsPath = Path.Combine(root, ".claude", "settings.json");
  string before = File.Exists(settingsPath) ? File.ReadAllText(settingsPath) : "";
  Dictionary<string, object?> settings;
  try {
    settings = before.Trim().Length > 0
      ? ZJson.DeserializeObject<Dictionary<string, object?>>(null, before, Lenient()) ?? new Dictionary<string, object?>()
      : new Dictionary<string, object?>();
  } catch (Exception e) {
    Console.Error.WriteLine($"[install] .claude/settings.json is not valid JSON: {e.Message}");
    return false;
  }

  if (settings.GetValueOrDefault("hooks") is not Dictionary<string, object?> events) settings["hooks"] = events = new Dictionary<string, object?>();

  // Strip every engine-owned entry first, then re-add from the manifest: one pass handles additions,
  // command/timeout edits, matcher moves and deletions alike, and converges however far behind the repo was.
  foreach (string evt in events.Keys.ToList()) {
    if (events[evt] is not List<object?> groups) continue;
    for (int g = groups.Count - 1; g >= 0; g--) {
      if (groups[g] is not Dictionary<string, object?> group || group.GetValueOrDefault("hooks") is not List<object?> entries) continue;
      entries.RemoveAll(e => Command(e).Contains(owned));
      if (entries.Count <= 0) groups.RemoveAt(g);   // a group that only ever held engine hooks.
    }
    if (groups.Count <= 0) events.Remove(evt);
  }

  foreach (var entry in declared) {
    string command = (entry.Command ?? "").Replace("$ENGINE/", engineIsRoot ? "" : engineRel + "/");
    if (string.IsNullOrEmpty(entry.Event) || command.Length <= 0) {
      Console.Error.WriteLine("[install] claude-hooks.json: an entry is missing its event or command.");
      return false;
    }
    if (Regex.Match(command, @"\$CLAUDE_PROJECT_DIR/([^""]+\.cs)") is { Success: true } script) scripts.Add(script.Groups[1].Value);

    if (events.GetValueOrDefault(entry.Event) is not List<object?> groups) events[entry.Event] = groups = new List<object?>();
    string matcher = entry.Matcher ?? "";
    var group = groups.OfType<Dictionary<string, object?>>().FirstOrDefault(g => (g.GetValueOrDefault("matcher") as string ?? "") == matcher);
    if (group == null) {
      group = new Dictionary<string, object?> { ["matcher"] = matcher, ["hooks"] = new List<object?>() };
      groups.Add(group);
    }
    if (group.GetValueOrDefault("hooks") is not List<object?> hooks) group["hooks"] = hooks = new List<object?>();

    var node = new Dictionary<string, object?> { ["type"] = "command", ["command"] = command };
    if (entry.Timeout is int t) node["timeout"] = t;
    // Engine guards run first within their group: they are the shared invariants, and a repo-specific
    // hook is cheaper to reach after the general ones have already rejected an edit.
    hooks.Insert(hooks.Count(e => Command(e).Contains(owned)), node);
  }

  string after = Write(settings) + "\n";
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

// ---------------------------------------------------------------------------------------------
// 3. Pre-build the hook scripts, one at a time
// ---------------------------------------------------------------------------------------------
void PrebuildHookScripts(List<string> scripts) {
  foreach (string script in scripts.Distinct()) {
    string full = Path.Combine(root, script);
    if (!File.Exists(full)) continue;
    var (code, output) = Run("dotnet", ["build", full, "-v", "q", "-nologo"]);
    if (code != 0) {
      Console.Error.WriteLine($"[install] could not pre-build {script} (the hook will build on first use):");
      Console.Error.WriteLine(output.TrimEnd());
    }
  }
  if (scripts.Count > 0) Console.WriteLine($"[install] {scripts.Distinct().Count()} hook script(s) pre-built.");
}

static string Command(object? entry) =>
  entry is Dictionary<string, object?> d && d.GetValueOrDefault("command") is string c ? c : "";

// Dictionaries rather than a model for settings.json: every key Claude Code or the repo keeps there must
// round-trip untouched. Relaxed escaping so the quotes inside a hook command stay `\"` rather than
// `"` — both are valid JSON and Claude Code reads either, but the escaped form churns the whole file
// on first write and is unreadable in review. "Unsafe" refers to HTML-embedding, not to files.
static string Write(object value) => ZJson.SerializeObject(value, new ZJsonSerializationOpts {
  PrettyPrint = true, UnsafeRelaxedEscaping = true, IgnoreNull = false,
});

static ZJsonSerializationOpts Lenient() => new ZJsonSerializationOpts { AllowCommentsAndTrailingCommas = true, ObjectsAsDictionaries = true };

static string Normalize(string json) {
  if (json.Trim().Length <= 0) return "";
  try {
    return Write(ZJson.DeserializeObject<Dictionary<string, object?>>(null, json, Lenient())!);
  } catch (Exception) {
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

/// <summary>`ci/claude-hooks.json`.</summary>
class HookManifest {
  public List<ManifestHook>? Hooks { get; set; }
}

class ManifestHook {
  public string? Event { get; set; }
  public string? Matcher { get; set; }
  public string? Command { get; set; }
  public int? Timeout { get; set; }
}

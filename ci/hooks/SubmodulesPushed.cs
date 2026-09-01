#!/usr/bin/env dotnet
// inzania-engine SubmodulesPushed — reusable git pre-push check, shared by every consuming repo.
// .NET 10 file-based app with a shebang: `chmod +x` and run it directly, or `dotnet run SubmodulesPushed.cs`.
//
// One rule: a superproject commit may not reach the remote before the submodule commit its gitlink
// points at. The two repos are pushed by separate commands, so the natural mistake is to push the
// superproject first (or to push it after rewriting the submodule's history out from under it). Nothing
// local notices: the working tree still has the object, `git status` is clean, and the build is green.
// The failure lands on whoever clones next — every CI agent — as
//     fatal: remote error: upload-pack: not our ref <sha>
// from `git fetch --recurse-submodules`, which fails the checkout before a single build step runs. It is
// also retroactive and permanent: the bad gitlink sits in pushed history, so *every* later build fails
// the same way until the missing object is pushed, no matter how many good commits land on top.
//
// So the check walks the commits actually being pushed, collects every gitlink value they record, and
// verifies each one is reachable from the submodule's own remote.
//
// WIRING (per repo): this cannot be symlinked to `.git/hooks/pre-push` directly — the kernel hands the
// shebang the path it was invoked by, and `dotnet` only accepts a file ending in `.cs`. Call it from the
// repo's own `pre-push` instead, forwarding the arguments and stdin git handed you:
//     dotnet run "$ROOT/inzania-engine/ci/hooks/SubmodulesPushed.cs" -- "$@" || exit 1
// Note most repos already have a `pre-push` owned by git-lfs; chain the two rather than replacing it.
//
// Run by hand (or in CI) with --all, which ignores stdin and checks the gitlinks recorded in HEAD.
//
// Bypass one push with SKIP_SUBMODULE_CHECK=1 git push ...
// Exit 0 = allow, 1 = block (reason on stderr).
using System.Diagnostics;

const string Zero = "0000000000000000000000000000000000000000";

if (Environment.GetEnvironmentVariable("SKIP_SUBMODULE_CHECK") is { Length: > 0 }) {
  Console.WriteLine("[submodules] skipped (SKIP_SUBMODULE_CHECK).");
  return 0;
}

string root = Run("git", ["rev-parse", "--show-toplevel"]).Output.Trim();
if (root.Length <= 0) {
  Console.Error.WriteLine("[submodules] not inside a git repository.");
  return 1;
}
Directory.SetCurrentDirectory(root);

// Paths come from .gitmodules rather than `git submodule foreach`, so a repo with no submodules costs
// one git call and a repo mid-clone (module not checked out yet) reports that instead of crashing.
var modules = Run("git", ["config", "--file", ".gitmodules", "--get-regexp", @"^submodule\..*\.path$"])
  .Output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
  .Select(l => l.Split(' ', 2))
  .Where(p => p.Length == 2)
  .Select(p => p[1].Trim())
  .ToList();
if (modules.Count <= 0) return 0;

// The ranges to inspect. Normally these come from git on stdin, one line per ref being pushed:
//     <local ref> <local sha> <remote ref> <remote sha>
var ranges = new List<(string Label, string[] RevArgs)>();
if (Args().Contains("--all")) {
  ranges.Add(("HEAD", ["--max-count=1", "HEAD"]));
} else {
  string remote = Args().FirstOrDefault() ?? "origin";
  foreach (string line in ReadStdin()) {
    var f = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
    if (f.Length < 4) continue;
    string localSha = f[1], remoteRef = f[2], remoteSha = f[3];
    if (localSha.All(c => c == '0')) continue;  // deleting a remote branch moves no gitlink.

    // A branch the remote does not have yet has no "since"; bound the walk by everything the remote is
    // already known to have, so a first push does not re-verify the whole history.
    ranges.Add(remoteSha.All(c => c == '0')
      ? ($"{remoteRef} (new)", [localSha, "--not", $"--remotes={remote}"])
      : ($"{remoteRef}", [$"{remoteSha}..{localSha}"]));
  }
  if (ranges.Count <= 0) ranges.Add(("HEAD", ["--max-count=1", "HEAD"]));
}

// gitlink value -> the submodule path it belongs to. A value recorded by several commits is checked once.
var wanted = new Dictionary<string, HashSet<string>>();
foreach (var (_, revArgs) in ranges) {
  var logArgs = new List<string> { "log", "--format=", "--raw", "--no-abbrev" };
  logArgs.AddRange(revArgs);
  logArgs.Add("--");
  logArgs.AddRange(modules);

  var log = Run("git", logArgs);
  if (log.Code != 0) {
    Console.Error.WriteLine(log.Output.TrimEnd());
    Console.Error.WriteLine("[submodules] could not list the commits being pushed — push aborted.");
    return 1;
  }

  // ":160000 160000 <old> <new> M\t<path>" — take <new> whenever the destination is still a gitlink.
  foreach (string line in log.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries)) {
    if (!line.StartsWith(':')) continue;
    var halves = line.Split('\t');
    if (halves.Length < 2) continue;
    var f = halves[0].Split(' ', StringSplitOptions.RemoveEmptyEntries);
    if (f.Length < 4 || f[1] != "160000") continue;  // f[1] is the destination mode; not 160000 = removed.
    string sha = f[3];
    if (sha == Zero) continue;

    string path = halves[^1].Trim();
    if (!wanted.TryGetValue(path, out var shas)) wanted[path] = shas = new HashSet<string>();
    shas.Add(sha);
  }
}

if (wanted.Count <= 0) {
  Console.WriteLine("[submodules] nothing being pushed moves a submodule pointer.");
  return 0;
}

var missing = new List<string>();
foreach (var (path, shas) in wanted) {
  if (!Directory.Exists(Path.Combine(root, path, ".git")) && !File.Exists(Path.Combine(root, path, ".git"))) {
    Console.Error.WriteLine($"[submodules] {path} is not checked out — run: git submodule update --init --recursive");
    return 1;
  }

  string remote = Run("git", ["-C", path, "remote"]).Output
    .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
    .FirstOrDefault(r => r == "origin")
    ?? Run("git", ["-C", path, "remote"]).Output
      .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
      .FirstOrDefault() ?? "origin";

  // Refresh the remote-tracking refs first: the question is what the remote has *now*, and a stale
  // local view would block a push whose submodule commit a teammate already pushed.
  Console.WriteLine($"[submodules] checking {path} against {remote}...");
  Run("git", ["-C", path, "fetch", "--quiet", remote]);

  foreach (string sha in shas) {
    if (Run("git", ["-C", path, "cat-file", "-e", $"{sha}^{{commit}}"]).Code != 0) {
      missing.Add($"  {path} {sha} — not in the local clone either; it was rewritten away or never existed.");
      continue;
    }
    // Anything reachable from sha but not from the remote's refs means the remote cannot serve sha.
    var ahead = Run("git", ["-C", path, "rev-list", "--max-count=1", sha, "--not", $"--remotes={remote}"]);
    if (ahead.Code == 0 && ahead.Output.Trim().Length <= 0) continue;
    missing.Add($"  {path} {sha} — {Describe(path, sha)}");
  }

  // Not fatal: the recorded gitlink is what CI checks out, so uncommitted work in the submodule cannot
  // break the build. It does mean the push carries less than it looks like it does, which is worth saying.
  if (Run("git", ["-C", path, "status", "--porcelain"]).Output.Trim().Length > 0)
    Console.WriteLine($"[submodules] note: {path} has uncommitted changes; they are not part of this push.");
}

if (missing.Count <= 0) {
  Console.WriteLine($"[submodules] every submodule pointer being pushed is on its remote.");
  return 0;
}

Console.Error.WriteLine($"[submodules] {missing.Count} submodule commit(s) are not on their remote — push aborted.");
Console.Error.WriteLine("[submodules] pushing this would make every fresh checkout fail with");
Console.Error.WriteLine("[submodules]   fatal: remote error: upload-pack: not our ref <sha>");
Console.Error.WriteLine("[submodules] and keep failing for every later commit until the object is pushed.");
foreach (string m in missing) Console.Error.WriteLine(m);
Console.Error.WriteLine("[submodules] push the submodule first, e.g.:");
foreach (string path in wanted.Keys) Console.Error.WriteLine($"  git -C {path} push");
Console.Error.WriteLine("[submodules] bypass once with SKIP_SUBMODULE_CHECK=1 git push ...");
return 1;

static string[] Args() => Environment.GetCommandLineArgs().Skip(1).ToArray();

static string[] ReadStdin() {
  if (Console.IsInputRedirected != true) return [];
  return Console.In.ReadToEnd().Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}

// Best-effort context for the failure line — which branch the commit is on locally, or its subject.
static string Describe(string path, string sha) {
  string subject = Run("git", ["-C", path, "log", "--format=%s", "--max-count=1", sha]).Output.Trim();
  return subject.Length > 0 ? $"local only (\"{subject}\")" : "local only.";
}

static (int Code, string Output) Run(string file, IEnumerable<string> args) {
  var psi = new ProcessStartInfo(file) { RedirectStandardOutput = true, RedirectStandardError = true };
  foreach (string a in args) psi.ArgumentList.Add(a);
  using var p = Process.Start(psi)!;
  var stdout = p.StandardOutput.ReadToEndAsync();
  var stderr = p.StandardError.ReadToEndAsync();
  p.WaitForExit();
  return (p.ExitCode, stdout.Result + stderr.Result);
}

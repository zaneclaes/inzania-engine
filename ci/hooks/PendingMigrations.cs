#!/usr/bin/env dotnet
#:project ../../ZCore/ZCore.csproj
// inzania-engine PendingMigrations — reusable git pre-commit check, shared by every consuming repo.
// .NET 10 file-based app with a shebang: `chmod +x` and run it directly, or `dotnet run PendingMigrations.cs`.
//
// One rule: a commit may not leave the EF model ahead of its migrations. `inzania-engine/Docs/data-design.md`
// §6 makes `dotnet ef migrations add` the only producer of schema changes, and the app applies migrations
// itself at start-up (`ZHostApp.PrepareAsync` -> `DataProvider.MigrateDatabaseAsync`). So a model change
// that lands without its migration does not fail loudly at commit or in review — it deploys a binary whose
// model describes columns the database does not have, and the failure surfaces as a running pod querying a
// column that isn't there. The `MigrationGuard.cs` Claude hook stops schema changes taking a route other
// than `dotnet ef`; it cannot see a model edited in an IDE, by a merge, or by anyone not driving Claude.
// This is that check, at the one point every change passes through.
//
// The test is exactly "would `dotnet ef migrations add X` be a no-op": `dotnet ef migrations
// has-pending-model-changes`, which diffs the compiled model against the committed `ModelSnapshot` and
// needs no database.
//
// WIRING (per repo): this cannot be symlinked to `.git/hooks/pre-commit` directly — the kernel hands the
// shebang the path it was invoked by, and `dotnet` only accepts a file ending in `.cs`. Call it from the
// repo's own `pre-commit` instead:
//     dotnet run "$ROOT/inzania-engine/ci/hooks/PendingMigrations.cs" || exit 1
//
// CONFIG: `<repo-root>/ci/migration-check.json` (override with --config or IZ_MIGRATION_CHECK_CONFIG).
//     {
//       "configuration": "Release",                       // build configuration passed to dotnet ef
//       "env": { "ASPNETCORE_ENVIRONMENT": "Development" }, // design-time environment
//       "paths": ["\\.(cs|csproj|props)$"],                // staged paths that make the check worth running
//       "reviewDoc": "TuneWeb/Server/Migrations/README.md", // pointed at in the failure message
//       "contexts": [
//         { "project": "TuneWeb/Server", "context": "TuneServerDbContext" }
//       ]
//     }
// `startupProject` defaults to `project`; `context` may be omitted when the project has exactly one.
// No config file = nothing to check (warn and pass), so vendoring the engine does not break a repo that
// has no EF model yet.
//
// Bypass one commit with SKIP_MIGRATION_CHECK=1 git commit ...
// Exit 0 = allow, 1 = block (reason on stderr).
using System.Diagnostics;
using System.Text.RegularExpressions;
using IZ.Core.Contexts;
using IZ.Core.Json;

const string PendingMarker = "Changes have been made to the model";
string[] defaultPaths = { @"\.(cs|csproj|props)$" };

if (Environment.GetEnvironmentVariable("SKIP_MIGRATION_CHECK") is { Length: > 0 }) {
  Console.WriteLine("[migrations] skipped (SKIP_MIGRATION_CHECK).");
  return 0;
}

string root = Run("git", "rev-parse --show-toplevel").Trim();
if (root.Length <= 0) {
  Console.Error.WriteLine("[migrations] not inside a git repository.");
  return 1;
}

string configPath = ArgValue("--config")
                    ?? Environment.GetEnvironmentVariable("IZ_MIGRATION_CHECK_CONFIG")
                    ?? Path.Combine(root, "ci", "migration-check.json");
if (!File.Exists(configPath)) {
  Console.Error.WriteLine($"[migrations] no config at {Rel(configPath)}; nothing checked. " +
                          "See the header of inzania-engine/ci/hooks/PendingMigrations.cs to configure.");
  return 0;
}

ZScriptApp.Start("PendingMigrations");
MigrationCheckConfig config;
try {
  config = ZJson.DeserializeObject<MigrationCheckConfig>(null, File.ReadAllText(configPath),
    new ZJsonSerializationOpts { AllowCommentsAndTrailingCommas = true }) ?? new MigrationCheckConfig();
} catch (Exception e) {
  Console.Error.WriteLine($"[migrations] {Rel(configPath)} is not valid JSON: {e.Message}");
  return 1;
}

var contexts = config.Contexts ?? new List<MigrationContext>();
if (contexts.Count <= 0) {
  Console.Error.WriteLine($"[migrations] {Rel(configPath)} lists no contexts; nothing checked.");
  return 0;
}

string configuration = config.Configuration ?? "Release";
var pathPatterns = config.Paths is { Count: > 0 }
  ? config.Paths.Where(p => p.Length > 0).ToArray()
  : defaultPaths;

// A docs-, content- or asset-only commit cannot move the model, and the check costs a build.
// --all forces it anyway (for CI, or after a merge that the hook never saw).
if (!Args().Contains("--all")) {
  var staged = Run("git", "diff --cached --name-only --diff-filter=ACMR")
    .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
  if (!staged.Any(f => pathPatterns.Any(p => Regex.IsMatch(f, p, RegexOptions.IgnoreCase)))) return 0;
}

var env = config.Env ?? new Dictionary<string, string>();

var built = new HashSet<string>();  // startup projects already built this run; the rest reuse the output.
var pending = new List<string>();
foreach (var entry in contexts) {
  string? project = entry.Project;
  if (string.IsNullOrEmpty(project)) {
    Console.Error.WriteLine($"[migrations] {Rel(configPath)}: a contexts[] entry has no project.");
    return 1;
  }
  string startup = entry.StartupProject ?? project;
  string? context = entry.Context;
  string label = context ?? project;

  var efArgs = new List<string> {
    "ef", "migrations", "has-pending-model-changes",
    "--project", project, "--startup-project", startup, "--configuration", configuration,
  };
  if (!string.IsNullOrEmpty(context)) { efArgs.Add("--context"); efArgs.Add(context); }
  if (!built.Add(startup)) efArgs.Add("--no-build");

  Console.WriteLine($"[migrations] checking {label}...");
  var (code, output) = RunFull("dotnet", efArgs, env);

  // A non-verdict failure is retried once, without reusing an earlier build. Concurrent MSBuild
  // nodes racing on the same output directory produce a build error that is gone a second later, and
  // a guard that blocks a good commit at random is a guard people learn to bypass. A real breakage
  // fails both times — and would already have failed the caller's own build before reaching here.
  if (code != 0 && !output.Contains(PendingMarker, StringComparison.OrdinalIgnoreCase)) {
    efArgs.Remove("--no-build");
    var retry = RunFull("dotnet", efArgs, env);
    code = retry.Code;
    output += retry.Output;
  }

  if (code == 0) continue;
  if (output.Contains(PendingMarker, StringComparison.OrdinalIgnoreCase)) {
    pending.Add($"  {label}: dotnet ef migrations add <PascalCaseName> " +
                $"--project {project} --startup-project {startup}" +
                (string.IsNullOrEmpty(context) ? "" : $" --context {context}") +
                $" --configuration {configuration}");
    continue;
  }
  // Not a verdict — the check itself could not run (build failure, missing design-time factory,
  // no dotnet-ef tool). Passing here would silently retire the guard, so block and say why.
  Console.Error.WriteLine(output.TrimEnd());
  Console.Error.WriteLine($"[migrations] could not check {label} (see above) — commit aborted.");
  Console.Error.WriteLine("[migrations] bypass once with SKIP_MIGRATION_CHECK=1 git commit ...");
  return 1;
}

if (pending.Count <= 0) {
  Console.WriteLine($"[migrations] {contexts.Count} context(s) match their migrations.");
  return 0;
}

Console.Error.WriteLine($"[migrations] {pending.Count} context(s) have model changes with no migration — commit aborted.");
Console.Error.WriteLine("[migrations] the model would deploy against a schema that lacks its columns. Generate the migration:");
foreach (string p in pending) Console.Error.WriteLine(p);
Console.Error.WriteLine($"[migrations] then review the generated Up() ({config.ReviewDoc ?? "the owning Migrations/README.md"}) " +
                        "and commit it with the model change.");
Console.Error.WriteLine("[migrations] bypass once with SKIP_MIGRATION_CHECK=1 git commit ...");
return 1;

string[] Args() => Environment.GetCommandLineArgs().Skip(1).ToArray();

string? ArgValue(string name) {
  var a = Args();
  int i = Array.IndexOf(a, name);
  return i >= 0 && i + 1 < a.Length ? a[i + 1] : null;
}

string Rel(string p) => Path.GetRelativePath(root, p);

static string Run(string file, string args) {
  var psi = new ProcessStartInfo(file) { RedirectStandardOutput = true, RedirectStandardError = true };
  foreach (string a in args.Split(' ', StringSplitOptions.RemoveEmptyEntries)) psi.ArgumentList.Add(a);
  using var p = Process.Start(psi)!;
  string output = p.StandardOutput.ReadToEnd();
  p.WaitForExit();
  return output;
}

// Streams nothing: `dotnet ef` is chatty and only matters when the answer is "no" or "broken".
static (int Code, string Output) RunFull(string file, IEnumerable<string> args, Dictionary<string, string> env) {
  var psi = new ProcessStartInfo(file) { RedirectStandardOutput = true, RedirectStandardError = true };
  foreach (string a in args) psi.ArgumentList.Add(a);
  foreach (var (k, v) in env) psi.Environment[k] = v;
  using var p = Process.Start(psi)!;
  var stdout = p.StandardOutput.ReadToEndAsync();
  var stderr = p.StandardError.ReadToEndAsync();
  p.WaitForExit();
  return (p.ExitCode, stdout.Result + stderr.Result);
}

/// <summary>`ci/migration-check.json` (see the header for the keys).</summary>
class MigrationCheckConfig {
  public string? Configuration { get; set; }
  public Dictionary<string, string>? Env { get; set; }
  public List<string>? Paths { get; set; }
  public string? ReviewDoc { get; set; }
  public List<MigrationContext>? Contexts { get; set; }
}

class MigrationContext {
  public string? Project { get; set; }
  public string? StartupProject { get; set; }
  public string? Context { get; set; }
}

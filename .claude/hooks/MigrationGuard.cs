// inzania-engine MigrationGuard — reusable Claude Code PreToolUse hook (Write|Edit|MultiEdit|Bash).
// .NET 10 file-based app. Wire it in a consuming project's .claude/settings.json:
//   dotnet run "$CLAUDE_PROJECT_DIR/inzania-engine/.claude/hooks/MigrationGuard.cs"
// Reads the hook JSON from stdin. Exit 0 = allow (warnings on stderr), exit 2 = BLOCK (reason on stderr).
//
// One rule, enforced on every path a schema change can take: the ONLY producer of schema changes is
// `dotnet ef migrations add <Name>` against the owning server project, which writes the migration
// *and* its ModelSnapshot from the model. Nothing else — not an editor, not a shell redirect, not a
// psql/mysql session, not raw DDL in application code — may change the schema, because anything the
// snapshot does not know about makes the next generated migration wrong.
// Rules doc: inzania-engine/Docs/data-design.md §6; per-project workflow: the owning
// Migrations/README.md (Chordzy: TuneWeb/Server/Migrations/README.md).
//
// BLOCK rules:
//  M1  Hand-written migration file: Write/Edit of any *.cs under a Migrations/ folder, or of a
//      *ModelSnapshot.cs anywhere.
//  M2  Shell write to a migration file: a mutating command (>, >>, tee, sed -i, cp, mv, rm, touch,
//      patch, truncate, install) whose target is a Migrations/*.cs path.
//  M3  Ad-hoc DDL through a SQL client: mysql/mariadb/mysqlsh/mycli invoked with CREATE/ALTER/DROP/
//      RENAME/TRUNCATE TABLE|INDEX|DATABASE|SCHEMA, or ADD/DROP/MODIFY/CHANGE COLUMN.
//  M4  DDL in application code: those statements inside ExecuteSqlRaw/ExecuteSqlInterpolated/
//      FromSqlRaw (any of their *Async twins), or a hand-written .sql file of DDL.
// WARN rules:
//  M5  `dotnet ef database update|drop`: migrations are applied by the app at start-up
//      (ZHostApp.PrepareAsync -> DataProvider.MigrateDatabaseAsync) on every replica; running it by
//      hand is only ever right against a local dev database.
//  M6  `migrationBuilder.Sql(...)` data migration: must be provider-correct and re-runnable
//      (two replicas can run it concurrently).
// Escape hatch: put `migration-guard: allow` in the edited content or in the command, with a comment
// saying why. Anything under `.claude/hooks/` is exempt so these hooks can be edited and tested.
// NOTE for editors of this file: the DDL/verb literals below live in consts near the top on purpose —
// the Bash rules match the *command being run*, so keep example commands out of shell one-liners.
using System.Text.Json;
using System.Text.RegularExpressions;

string input = Console.In.ReadToEnd();
if (string.IsNullOrWhiteSpace(input)) return 0;
JsonDocument doc;
try { doc = JsonDocument.Parse(input); } catch { return 0; }
var root = doc.RootElement;
string tool = root.TryGetProperty("tool_name", out var tn) ? tn.GetString() ?? "" : "";
if (!root.TryGetProperty("tool_input", out var ti)) return 0;
string path = ti.TryGetProperty("file_path", out var fp) ? fp.GetString() ?? "" : "";
string command = ti.TryGetProperty("command", out var cm) ? cm.GetString() ?? "" : "";
string content = "";
if (ti.TryGetProperty("content", out var c)) content = c.GetString() ?? "";
else if (ti.TryGetProperty("new_string", out var ns)) content = ns.GetString() ?? "";
else if (ti.TryGetProperty("edits", out var edits) && edits.ValueKind == JsonValueKind.Array)
  content = string.Join("\n", edits.EnumerateArray().Select(e => e.TryGetProperty("new_string", out var s) ? s.GetString() ?? "" : ""));
string norm = path.Replace('\\', '/');

// Exemptions: the hooks themselves, and an explicitly justified escape hatch.
const string Allow = "migration-guard: allow";
if (norm.Contains("/.claude/hooks/") || norm.StartsWith(".claude/hooks/")) return 0;
if (command.Contains(".claude/hooks/")) return 0;
if (content.Contains(Allow) || command.Contains(Allow)) return 0;

const string Ddl = @"\b(CREATE|ALTER|DROP|RENAME|TRUNCATE)\s+(TABLE|INDEX|DATABASE|SCHEMA|COLUMN)\b|\b(ADD|DROP|MODIFY|CHANGE)\s+COLUMN\b";
const string Fix = "Change the [Table] model instead, then run `dotnet ef migrations add <PascalCaseName> --project <ServerProject> --startup-project <ServerProject> -c Release` so EF regenerates the migration and the ModelSnapshot together.";
var ddlRx = new Regex(Ddl, RegexOptions.IgnoreCase);
var blocks = new List<string>();
var warns = new List<string>();

// ---- File edits (Write|Edit|MultiEdit) ----
if (norm.Length > 0) {
  // M1 — hand-written migration / snapshot
  if (Regex.IsMatch(norm, @"(^|/)Migrations/.*\.cs$", RegexOptions.IgnoreCase) ||
      Regex.IsMatch(norm, @"ModelSnapshot\.cs$", RegexOptions.IgnoreCase))
    blocks.Add($"`{path}` is EF-generated. Hand-written or hand-edited migrations desync the ModelSnapshot from the model, and every later migration is then generated against a schema that does not exist. {Fix} To undo the last unapplied one use `dotnet ef migrations remove --force`; to inspect the SQL use `dotnet ef migrations script`.");

  // M4 — DDL in application code / a hand-rolled .sql schema file
  if (norm.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) && content.Length > 0) {
    foreach (Match m in Regex.Matches(content, @"\.(ExecuteSqlRaw|ExecuteSqlRawAsync|ExecuteSqlInterpolated|ExecuteSqlInterpolatedAsync|FromSqlRaw|FromSqlInterpolated)\s*\(([^;]*)", RegexOptions.Singleline)) {
      if (ddlRx.IsMatch(m.Groups[2].Value))
        blocks.Add($"DDL executed from application code (`{Trim(m.Value)}`). Schema changes must be migrations so they are versioned, ordered and applied once per deploy. {Fix}");
    }
    foreach (Match m in Regex.Matches(content, @"migrationBuilder\.Sql\s*\(", RegexOptions.IgnoreCase))
      warns.Add("`migrationBuilder.Sql` data migration: keep it provider-specific-correct and re-runnable — replicas can run it concurrently at start-up.");
  }
  if (norm.EndsWith(".sql", StringComparison.OrdinalIgnoreCase) && ddlRx.IsMatch(content))
    blocks.Add($"`{path}` is a hand-rolled DDL script. The schema has exactly one source of truth: the models plus the EF migrations generated from them. {Fix} If this file is a read-only artifact (e.g. `dotnet ef migrations script` output), write it outside the repo or add `{Allow}` with a reason.");
}

// ---- Shell commands (Bash) ----
if (command.Length > 0) {
  // M2 — a mutating command aimed at a migration file
  var shellWrite = new Regex(@"(>>?|\btee\b|\bsed\s+-i|\bcp\b|\bmv\b|\brm\b|\btouch\b|\bpatch\b|\btruncate\b|\binstall\b)[^;|&\n]{0,120}?(\S*Migrations/\S*\.cs)", RegexOptions.IgnoreCase);
  foreach (Match m in shellWrite.Matches(command))
    blocks.Add($"Shell write to a migration file (`{Trim(m.Value)}`). Migration files are only ever produced by `dotnet ef migrations add`, and deleted by `dotnet ef migrations remove --force` (which also rewinds the ModelSnapshot). {Fix}");

  // M3 — ad-hoc DDL through a SQL client
  var sqlClient = new Regex(@"(?m)(^|[;|&(]\s*|\bxargs\s+|\bsudo\s+)(mysql|mariadb|mysqlsh|mycli|psql)\s+-");
  if (sqlClient.IsMatch(command) && ddlRx.IsMatch(command)) {
    var m = ddlRx.Match(command);
    blocks.Add($"Ad-hoc DDL against a database (`{Trim(m.Value)}`). A schema change applied by hand exists in exactly one environment and is invisible to the ModelSnapshot, so the next generated migration will try to create it again. {Fix} Migrations are applied automatically at app start-up on every replica.");
  }

  // M5 — applying migrations by hand
  if (Regex.IsMatch(command, @"\bdotnet\s+ef\s+database\s+(update|drop)\b"))
    warns.Add("`dotnet ef database update|drop` applies/destroys a schema outside the deploy path — the app migrates itself at start-up. Only run this against a local development database, never a shared/staging/production one.");
}

foreach (var w in warns.Distinct()) Console.Error.WriteLine("migration-guard WARNING: " + w);
if (blocks.Count > 0) {
  Console.Error.WriteLine("migration-guard BLOCKED " + (tool.Length > 0 ? tool : "edit") + (path.Length > 0 ? " to " + path : ""));
  foreach (var b in blocks.Distinct()) Console.Error.WriteLine("  - " + b);
  return 2;
}
return 0;

static string Trim(string s) { s = Regex.Replace(s, @"\s+", " ").Trim(); return s.Length > 90 ? s[..90] + "…" : s; }

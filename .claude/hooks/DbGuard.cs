// inzania-engine DbGuard — reusable Claude Code PreToolUse hook (Write|Edit|MultiEdit).
// .NET 10 file-based app. Wire it in a consuming project's .claude/settings.json:
//   dotnet run "$CLAUDE_PROJECT_DIR/inzania-engine/.claude/hooks/DbGuard.cs"
// Reads the hook JSON from stdin. Exit 0 = allow (warnings on stderr), exit 2 = BLOCK (reason on stderr).
// The rules are documented in inzania-engine/Docs/data-design.md; whole-repo checks the per-edit
// heuristics can't do live in IndexAudit.cs next to this file.
//
// BLOCK rules:
//  B1  Hand-written EF Core migrations: any *.cs under a Migrations/ folder.
//  B2  DB call (Resolve*/Load*Async/QueryFor) inside a foreach/for/while body => N+1.
//  B3  Persisted bool column on an entity model — booleans are bits in a [Flags] enum column,
//      never their own column.
// WARN rules:
//  W1  Materialize-then-filter (.ToList()/LoadDataModelsAsync() then .Where/.First/...).
//  W2  .Count() > 0 instead of .Any().
//  W3  Leading-wildcard LIKE/Contains.
//  W4  .ToLower()/.ToUpper()/.Trim() on the column side of a predicate (non-sargable).
//  W5  4+ chained Include/Fetch levels.
//  W6  Unbounded whole-table LoadDataModelsAsync.
//  W7  New [Table] model with no [ApiIndex]/[ApiKey].
//  W8  Bitwise flag test (& / HasFlag) inside a Filter/Where predicate (non-sargable).
//  W9  [ApiIndex] over a *Flag* column (useless for bitwise predicates — pure write cost).
//  W10 Bitfield enum (`1 << n` members) missing the [Flags] attribute.
//  W11 [Flags] enum property on a model missing [OutputIgnore]/[InputIgnore] (HotChocolate
//      cannot (de)serialize flag combinations; only a numeric *Val mirror may cross the wire).
//  W12 String concatenation on the column side inside a Filter/Where predicate (non-sargable).
// Escape hatch for any rule: append `// db-guard: allow` on the flagged line + justify in a comment.
// NOTE for editors of this file: keep regex/message literals that mention the guarded APIs out of
// loop bodies (in the consts/locals below), and give every loop an explicit braced body — the
// line-based tracker treats a braceless loop as extending to the end of the enclosing block.
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
string content = "";
if (ti.TryGetProperty("content", out var c)) content = c.GetString() ?? "";
else if (ti.TryGetProperty("new_string", out var ns)) content = ns.GetString() ?? "";
else if (ti.TryGetProperty("edits", out var edits) && edits.ValueKind == JsonValueKind.Array)
  content = string.Join("\n", edits.EnumerateArray().Select(e => e.TryGetProperty("new_string", out var s) ? s.GetString() ?? "" : ""));
string norm = path.Replace('\\', '/');
var blocks = new List<string>();
var warns = new List<string>();
const string Doc = "inzania-engine/Docs/data-design.md";

// B1 — migrations
if (Regex.IsMatch(norm, @"(^|/)Migrations/[^/]*\.cs$", RegexOptions.IgnoreCase))
  blocks.Add("Hand-written EF Core migrations are not allowed. Run `dotnet ef migrations add <Name>` against the owning server project so EF generates the migration + ModelSnapshot. Never edit files under Migrations/ by hand.");

if (norm.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) && content.Length > 0) {
  string code = Regex.Replace(content, @"//[^\n]*", "");           // strip line comments
  code = Regex.Replace(code, @"/\*.*?\*/", "", RegexOptions.Singleline);
  string[] lines = code.Split('\n');
  string[] rawLines = content.Split('\n');                          // raw text: `db-guard: allow` lives in comments

  // All regexes + message builders up front (see NOTE above).
  var dbCall = new Regex(@"\b(Resolve(LocalId|Array|ForeignId|Key)|Load(DataModels?|DataModel|Scalar|Scalars|Count|Lookup|Dictionary|LongSum|DoubleSum|ModelId|RequiredModelId)Async|LoadModelId|QueryFor<)\b|\.LoadDataModel");
  var loopHead = new Regex(@"^\s*(foreach|for|while)\s*\(");
  var boolProp = new Regex(@"public\s+(?:override\s+|virtual\s+|new\s+)?bool\??\s+(\w+)\s*\{\s*get;\s*(?:private\s+|protected\s+|internal\s+)?(?:set|init);");
  var w1Materialize = new Regex(@"(LoadDataModelsAsync\(\)|LoadScalarsAsync\(\)|\.ToListAsync\([^)]*\)|\.ToList\(\)|\.ToArray\(\))\s*\)?\s*\.(Where|Count|First|FirstOrDefault|Any|Single|OrderBy|Skip|Take)\(");
  var w2Count = new Regex(@"\.Count\(\)\s*(>\s*0|!=\s*0|>=\s*1)|\.LoadCountAsync\(\)\s*(>\s*0|!=\s*0|>=\s*1)");
  var w3Wildcard = new Regex(@"(Contains|Like)\(\s*[^)]*""%");
  var w4Transform = new Regex(@"\.(Filter|Where)\(\s*\w+\s*=>[^;]*?\.(ToLower|ToUpper|Trim)\(\)\s*(==|!=|\.Equals)");
  var w5Includes = new Regex(@"(\.(Fetch|Include|ThenInclude|ThenFetch|QueryInclude|QueryThenInclude|QueryThenIncludeMany)\([^;]*?){4,}");
  var w6Unbounded = new Regex(@"QueryFor<\w+>\(\)((?:\s*\.\w+\([^;]*?\))*?)\s*\.LoadDataModelsAsync\(\)", RegexOptions.Singleline);
  var w6Bounded = new Regex(@"\.(Filter|Where|Limit|Take|SortAsc|SortDsc|OrderBy)");
  var w7Table = new Regex(@"\[Table\(""(\w+)""\)\]([^{]*?)\bclass\s+(\w+)", RegexOptions.Singleline);
  var w8Bitwise = new Regex(@"\.(Filter|Where)\(\s*\w+\s*=>[^;]*?((?<![&=])&(?![&=])|\.HasFlag\()");
  var w9FlagIndex = new Regex(@"\[ApiIndex\([^\]]*?(nameof\(\s*[\w.]*Flag\w*\s*\)|""\w*Flag\w*"")");
  var w10Enum = new Regex(@"enum\s+(\w+)[^{]*\{([^}]*)\}", RegexOptions.Singleline);
  var w10FlagsAttr = new Regex(@"\[\s*Flags\s*\]\s*(\[[^\]]*\]\s*)*(public|internal)?\s*$");
  var w11FlagEnumDecl = new Regex(@"\[\s*Flags\s*\][^{;]*?enum\s+(\w+)", RegexOptions.Singleline);
  var w12Concat = new Regex(@"\.(Filter|Where)\(\s*\w+\s*=>[^;]*?\b\w+\.\w+\s*\+\s*""");
  string n1Msg(int lineNo, string ln) =>
    $"Line {lineNo}: database call inside a loop (`{ln.Trim()}`). This is an N+1 query. Batch it: load all rows first with one `QueryFor<T>().Filter(x => keys.Contains(x.Key))`, or `.Fetch()`/`QueryInclude`, or use `Context.Resolver.LoadArray/LoadAll` (batched under GraphQL). If it is intentional, append `// db-guard: allow` on that line and justify in a comment.";
  string w1Msg(string frag) =>
    $"Materializing then filtering in memory (`{frag}`): move the predicate/ordering/paging into the query (`.Filter/.SortAsc/.Limit`) so MySQL does it with an index.";
  string w6Msg(string frag) =>
    $"Unbounded load of an entire table (`{frag}`): add a `.Filter(...)` and/or `.Limit(n)`; tables grow.";

  // B2 — N+1: a DB call inside a loop body. Track brace depth after a loop header.
  var loopDepths = new Stack<int>();
  int depth = 0;
  for (int i = 0; i < lines.Length; i++) {
    string ln = lines[i];
    bool allowed = i < rawLines.Length && rawLines[i].Contains("db-guard: allow");
    if (loopHead.IsMatch(ln)) { loopDepths.Push(depth); }
    if (loopDepths.Count > 0 && dbCall.IsMatch(ln) && !allowed) { blocks.Add(n1Msg(i + 1, ln)); }
    foreach (char ch in ln) {
      if (ch == '{') { depth++; }
      else if (ch == '}') { depth--; while (loopDepths.Count > 0 && depth <= loopDepths.Peek()) { loopDepths.Pop(); } }
    }
  }

  // Is this file an entity-model file? ([Table] present, or a class extending the storable bases)
  bool isModelFile = code.Contains("[Table(") ||
    Regex.IsMatch(code, @"\bclass\s+\w+[^{;]*:\s*[^{;]*\b(DataObject|ModelId|ModelNumber|ModelKey\b|ModelKey<)");

  // B3 — persisted bool column on an entity
  if (isModelFile) {
    for (int i = 0; i < lines.Length; i++) {
      var m = boolProp.Match(lines[i]);
      if (!m.Success) { continue; }
      if (i < rawLines.Length && rawLines[i].Contains("db-guard: allow")) { continue; }
      string before = string.Join("\n", lines.Skip(Math.Max(0, i - 4)).Take(Math.Min(4, i)));
      if (before.Contains("NotMapped")) { continue; }
      blocks.Add($"Line {i + 1}: persisted bool column `{m.Groups[1].Value}`. Never store a raw bool: add a bit to the model's [Flags] enum column and expose a read-only accessor (`public bool {m.Groups[1].Value} => Flags.HasFlag(...)`), transmitting the bitfield as a numeric *Val property. See {Doc} §1. For a runtime-only value add [NotMapped]; if truly intentional, append `// db-guard: allow` and justify.");
    }
  }

  // W1 — in-memory filtering after materialization
  foreach (Match m in w1Materialize.Matches(code)) { warns.Add(w1Msg(Trim(m.Value))); }

  // W2 — Count() > 0
  if (w2Count.IsMatch(code))
    warns.Add("Use `.Any()` / an existence query instead of a materialized count compared to zero — COUNT scans every matching row.");

  // W3 — leading wildcard
  foreach (Match m in w3Wildcard.Matches(code)) {
    warns.Add($"Leading-wildcard LIKE (`{Trim(m.Value)}`) cannot use an index; prefer prefix match (StartsWith), a normalized lookup column, or full-text search.");
  }

  // W4 — non-sargable transforms inside query lambdas
  foreach (Match m in w4Transform.Matches(code)) {
    warns.Add($"Transforming a column inside a predicate (`{Trim(m.Value)}`) defeats the index; compare against a pre-normalized indexed column (e.g. `UsernameLower`) and normalize the parameter instead.");
  }

  // W5 — deep include chains
  foreach (Match m in w5Includes.Matches(code)) {
    warns.Add("4+ chained Include/Fetch on one query: with SplitQuery each level is another round-trip and wide joins explode row counts; load the deepest level with a separate keyed batch instead.");
  }

  // W6 — unbounded loads: whole-table load with no Filter/Limit/Take in the chain
  foreach (Match m in w6Unbounded.Matches(code)) {
    if (!w6Bounded.IsMatch(m.Groups[1].Value)) { warns.Add(w6Msg(Trim(m.Value))); }
  }

  // W7 — new [Table] without index
  foreach (Match m in w7Table.Matches(code)) {
    string attrs = m.Groups[2].Value;
    int start = Math.Max(0, m.Index - 600);
    string before = code.Substring(start, m.Index - start);
    if (!attrs.Contains("ApiIndex") && !before.Contains("ApiIndex") && !attrs.Contains("ApiKey") && !before.Contains("ApiKey"))
      warns.Add($"Model `{m.Groups[3].Value}` ([Table(\"{m.Groups[1].Value}\")]) declares no [ApiIndex]/[ApiKey]. Every table needs indexes for the columns you filter/sort on (FK columns and CreatedAt/UpdatedAt are auto-indexed; anything else is not). See {Doc} §2.");
  }

  // W8 — bitwise flag test inside a predicate (single & or .HasFlag, not &&)
  foreach (Match m in w8Bitwise.Matches(code)) {
    warns.Add($"Bitwise flag test inside a predicate (`{Trim(m.Value)}`): a B-tree index cannot serve a bit test; on a growing table this is a full scan. Filter on an indexed column, or promote the hot bit to its own column. See {Doc} §1.");
  }

  // W9 — [ApiIndex] over a flags column
  foreach (Match m in w9FlagIndex.Matches(code)) {
    warns.Add($"[ApiIndex] over a flags column (`{Trim(m.Value)}`): bitwise predicates cannot use it — the index is pure write cost. Remove it; if a bit is a hot filter, give it its own indexed column. See {Doc} §1.");
  }

  // W10 — bitfield enum missing [Flags]
  foreach (Match m in w10Enum.Matches(code)) {
    if (!m.Groups[2].Value.Contains("<<")) { continue; }
    int start = Math.Max(0, m.Index - 300);
    string before = code.Substring(start, m.Index - start);
    if (!w10FlagsAttr.IsMatch(before))
      warns.Add($"Enum `{m.Groups[1].Value}` has bit-shifted members but no [Flags] attribute — HasFlag/ToString and serialization treat it as a plain enum. Add [Flags] (and an explicit base type). See {Doc} §1.");
  }

  // W11 — [Flags] enum property without [OutputIgnore]/[InputIgnore]
  var flagEnums = w11FlagEnumDecl.Matches(code).Select(m => m.Groups[1].Value).Distinct().ToList();
  foreach (string fe in flagEnums) {
    foreach (Match m in Regex.Matches(code, @"public\s+" + Regex.Escape(fe) + @"\??\s+(\w+)\s*\{\s*get;\s*set;")) {
      int start = Math.Max(0, m.Index - 250);
      string before = code.Substring(start, m.Index - start);
      var missing = new List<string>();
      if (!before.Contains("OutputIgnore")) { missing.Add("[OutputIgnore]"); }
      if (!before.Contains("InputIgnore")) { missing.Add("[InputIgnore]"); }
      if (missing.Count > 0)
        warns.Add($"[Flags] property `{m.Groups[1].Value}` ({fe}) is missing {string.Join(" + ", missing)}: HotChocolate throws on flag combinations, so the enum must never cross the GraphQL wire — expose a [NotMapped] numeric *Val mirror instead. See {Doc} §1.");
    }
  }

  // W12 — string concatenation on the column side inside a predicate
  foreach (Match m in w12Concat.Matches(code)) {
    warns.Add($"String concatenation on the column side of a predicate (`{Trim(m.Value)}`): not sargable — filter each component column separately (e.g. per-component key sets with a composite [ApiIndex]). See {Doc} §2.");
  }
}

foreach (var w in warns.Distinct()) Console.Error.WriteLine("db-guard WARNING: " + w);
if (blocks.Count > 0) {
  Console.Error.WriteLine("db-guard BLOCKED " + (tool.Length > 0 ? tool : "edit") + " to " + path);
  foreach (var b in blocks.Distinct()) Console.Error.WriteLine("  - " + b);
  return 2;
}
return 0;

static string Trim(string s) { s = Regex.Replace(s, @"\s+", " ").Trim(); return s.Length > 90 ? s[..90] + "…" : s; }

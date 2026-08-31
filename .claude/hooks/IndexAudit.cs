// inzania-engine IndexAudit — whole-repo database-design audit (.NET 10 file-based app).
//   dotnet run inzania-engine/.claude/hooks/IndexAudit.cs -- <repo-root> [--strict]
//   dotnet run inzania-engine/.claude/hooks/IndexAudit.cs -- --hook   (Claude Code PostToolUse)
// Heuristic, regex-based cross-reference of the rules in inzania-engine/Docs/data-design.md that a
// per-edit hook cannot check: it builds the model map ([Table] classes, their properties, declared
// [ApiIndex]/[ApiKey], inherited indexes, auto-indexed columns) and then verifies every
// Filter/SortAsc/FilterKeyIn query site against it, plus the cross-file [Flags] rules (which also
// cover wire-only ApiObject/TransientObject classes).
// Advisory by default (exit 0); --strict exits 1 when findings exist.
// --hook mode: reads the PostToolUse JSON from stdin and exits 0 immediately unless the edited
// file is .cs AND the edit *introduces* query surface or index-shaping attributes (QueryFor /
// Filter / Sort / FilterKeyIn / [ApiIndex] / [ApiKey] / [Table] present in the new content and,
// for Edits, more of them than in the replaced text). Only then does it run the full audit,
// rooted at $CLAUDE_PROJECT_DIR; NEW (non-baselined) findings are reported on stderr with exit 2
// so they are fed back to the model. Wire it as PostToolUse (matcher Write|Edit|MultiEdit).
// Known approximations (by design — this is a linter, not a compiler):
//  - properties/attributes are associated to the nearest preceding class declaration in the file;
//  - every property ending in `Id` is assumed FK-auto-indexed;
//  - a query is "covered" when ANY declared/auto index leads with one of its filter columns
//    (or its sort column when there is no filter) — column order quality is not verified;
//  - navigation/collection properties inside predicates are ignored (they become SQL joins).
using System.Text.Json;
using System.Text.RegularExpressions;

var roots = new List<string>();
bool strict = false, hookMode = false;
foreach (string a in args) {
  if (a == "--strict") { strict = true; } else if (a == "--hook") { hookMode = true; } else { roots.Add(a); }
}

// Gate patterns: an edit is audit-worthy when it adds query surface or index-shaping attributes.
var gateRx = new Regex(@"QueryFor<|\.Filter\(|\.Where\(|\.SortAsc\(|\.SortDsc\(|\.FilterKeyIn\(|\[ApiIndex|\[ApiKey|\[Table\(");
if (hookMode) {
  string stdin = Console.In.ReadToEnd();
  if (string.IsNullOrWhiteSpace(stdin)) return 0;
  JsonDocument hookDoc;
  try { hookDoc = JsonDocument.Parse(stdin); } catch { return 0; }
  if (!hookDoc.RootElement.TryGetProperty("tool_input", out var hti)) return 0;
  string hookPath = hti.TryGetProperty("file_path", out var hfp) ? hfp.GetString() ?? "" : "";
  if (!hookPath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)) return 0;
  string newText = "", oldText = "";
  if (hti.TryGetProperty("content", out var hc)) { newText = hc.GetString() ?? ""; }
  else if (hti.TryGetProperty("new_string", out var hns)) {
    newText = hns.GetString() ?? "";
    if (hti.TryGetProperty("old_string", out var hos)) oldText = hos.GetString() ?? "";
  } else if (hti.TryGetProperty("edits", out var hEdits) && hEdits.ValueKind == JsonValueKind.Array) {
    foreach (var e in hEdits.EnumerateArray()) {
      newText += (e.TryGetProperty("new_string", out var en) ? en.GetString() : "") + "\n";
      oldText += (e.TryGetProperty("old_string", out var eo) ? eo.GetString() : "") + "\n";
    }
  }
  // "New query": more gate matches in the new text than in the text it replaced.
  if (gateRx.Matches(newText).Count <= gateRx.Matches(oldText).Count) return 0;
  string projectDir = Environment.GetEnvironmentVariable("CLAUDE_PROJECT_DIR") ?? Directory.GetCurrentDirectory();
  roots.Clear();
  roots.Add(projectDir);
}
if (roots.Count == 0) roots.Add(Directory.GetCurrentDirectory());

string[] skipDirs = { "obj", "bin", "out", "node_modules", ".git", "Temp", "Library", "Logs", ".claude", "Migrations", "Generated" };
string[] wireBases = { "ApiObject", "TransientObject", "DataObject", "ModelId", "ModelNumber", "ModelKey", "ZPacket" };

// ---- regexes (top-level so DbGuard's loop heuristics never misread this file) ----
var classRx = new Regex(@"(?:(?:public|internal|private|protected|abstract|sealed|partial|static)[ \t]+)*class[ \t]+(\w+)(?:[ \t]*:[ \t]*([^{\r\n]+))?");
var tableRx = new Regex(@"\[Table\(""(\w+)""\)\]");
var apiIndexRx = new Regex(@"\[ApiIndex\(([^\]]*?)\)\]");
var apiKeyRx = new Regex(@"\[ApiKey\(([^\]]*?)\)\]");
var colRefRx = new Regex(@"nameof\(\s*(?:\w+\.)?(\w+)\s*\)|""(\w+)""");
var propRx = new Regex(@"public[ \t]+(?:override[ \t]+|virtual[ \t]+|new[ \t]+|required[ \t]+)*([\w?<>\[\],. ]+?)[ \t]+(\w+)[ \t]*\{[ \t]*get;[ \t]*(?:private[ \t]+|protected[ \t]+|internal[ \t]+)?(?:set|init);");
var flagsEnumRx = new Regex(@"\[\s*Flags\s*\]\s*(?:\[[^\]]*\]\s*)*(?:public|internal)?\s*enum\s+(\w+)(?:\s*:\s*(\w+))?");
var anyEnumRx = new Regex(@"enum\s+(\w+)(?:\s*:\s*\w+)?[^{]*\{([^}]*)\}", RegexOptions.Singleline);
var queryForRx = new Regex(@"QueryFor<(\w+)>\(\)");
var filterLambdaRx = new Regex(@"\.(Filter|Where)\(\s*(\w+)\s*=>");
var sortRx = new Regex(@"\.(SortAsc|SortDsc|OrderBy|OrderByDescending)\(\s*(\w+)\s*=>\s*\2\.(\w+)");
var filterKeyInRx = new Regex(@"\.FilterKeyIn\(\s*(?:nameof\(\s*(?:\w+\.)?(\w+)\s*\)|""(\w+)"")");
var navTypeRx = new Regex(@"^(List<|IList<|ICollection<|IEnumerable<|HashSet<)|\[\]$");

// Attributes for a declaration = text between the previous `;`/`{`/`}` and the declaration start.
// Handles attributes on their own lines, inline before the keyword, and trailing comments.
static string AttrsBefore(string src, int declIndex) {
  int b = declIndex - 1;
  while (b >= 0 && src[b] is not (';' or '{' or '}')) { b--; }
  return src[(b + 1)..declIndex];
}

// ---- pass 1: collect files ----
var files = new List<string>();
var seenReal = new HashSet<string>();
void Walk(string dir) {
  var di = new DirectoryInfo(dir);
  if ((di.Attributes & FileAttributes.ReparsePoint) != 0) return;   // skip symlinks (Unity mirrors)
  if (skipDirs.Contains(di.Name)) return;
  if (!seenReal.Add(di.FullName)) return;
  foreach (var f in di.GetFiles("*.cs")) {
    if ((f.Attributes & FileAttributes.ReparsePoint) == 0) files.Add(f.FullName);
  }
  foreach (var d in di.GetDirectories()) { Walk(d.FullName); }
}
foreach (string r in roots) { Walk(Path.GetFullPath(r)); }

// ---- pass 2: model map ----
var classes = new Dictionary<string, ModelClass>();                  // name -> info (last one wins on dupes)
var flagsEnums = new Dictionary<string, string>();                   // enum name -> base type ("" unknown)
var findings = new List<string>();

var sources = new Dictionary<string, string>();
foreach (string file in files) {                                     // pass 2a: all enums first, so
  string src = File.ReadAllText(file);                               // consumers in earlier files see them
  sources[file] = src;
  foreach (Match fe in flagsEnumRx.Matches(src)) { flagsEnums[fe.Groups[1].Value] = fe.Groups[2].Value; }
  foreach (Match em in anyEnumRx.Matches(src)) {
    if (em.Groups[2].Value.Contains("<<") && !flagsEnums.ContainsKey(em.Groups[1].Value) && !AttrsBefore(src, em.Index).Contains("[Flags]"))
      findings.Add($"[flags] enum `{em.Groups[1].Value}` has bit-shifted members but no [Flags] attribute — {Rel(file)}");
  }
}
foreach (string file in files) {
  string src = sources[file];
  var classMatches = classRx.Matches(src).Where(m => AttrsBefore(src, m.Index).TrimEnd().EndsWith("]") || m.Index == 0 || !char.IsLetterOrDigit(src[Math.Max(0, m.Index - 1)])).ToList();
  for (int ci = 0; ci < classMatches.Count; ci++) {
    var cm = classMatches[ci];
    string attrs = AttrsBefore(src, cm.Index);
    var mc = new ModelClass {
      Name = cm.Groups[1].Value, File = file, IsAbstract = cm.Value.Contains("abstract"),
      Table = tableRx.Match(attrs) is { Success: true } t ? t.Groups[1].Value : null,
      Bases = (cm.Groups[2].Success ? cm.Groups[2].Value : "").Split(',').Select(s => s.Trim().Split('<')[0].Trim()).Where(s => s.Length > 0).ToList(),
    };
    foreach (Match ix in apiIndexRx.Matches(attrs)) {
      var cols = colRefRx.Matches(ix.Groups[1].Value).Select(g => g.Groups[1].Success ? g.Groups[1].Value : g.Groups[2].Value).ToList();
      if (cols.Count > 0) mc.Indexes.Add(cols);
    }
    foreach (Match k in apiKeyRx.Matches(attrs)) {
      var cols = colRefRx.Matches(k.Groups[1].Value).Select(g => g.Groups[1].Success ? g.Groups[1].Value : g.Groups[2].Value).ToList();
      if (cols.Count > 0) mc.Indexes.Add(cols);                      // composite PK = leading index
    }
    int bodyStart = cm.Index + cm.Length;
    int bodyEnd = ci + 1 < classMatches.Count ? classMatches[ci + 1].Index : src.Length;
    foreach (Match pm in propRx.Matches(src[bodyStart..bodyEnd])) {
      int absIndex = bodyStart + pm.Index;
      string pAttrs = AttrsBefore(src, absIndex);
      string pType = pm.Groups[1].Value.Trim(), pName = pm.Groups[2].Value;
      bool notMapped = pAttrs.Contains("NotMapped");
      mc.Props[pName] = pType;
      int lineNo = src[..absIndex].Count(chr => chr == '\n') + 1;
      if (!notMapped && (pType == "bool" || pType == "bool?"))
        mc.BoolProps.Add((pName, lineNo));
      if (flagsEnums.ContainsKey(pType.TrimEnd('?'))) {
        var missing = new List<string>();
        if (!pAttrs.Contains("OutputIgnore")) { missing.Add("[OutputIgnore]"); }
        if (!pAttrs.Contains("InputIgnore")) { missing.Add("[InputIgnore]"); }
        if (missing.Count > 0) mc.FlagLeaks.Add((pName, pType.TrimEnd('?'), string.Join(" + ", missing), lineNo));
      }
    }
    classes[mc.Name] = mc;
  }
}

IEnumerable<ModelClass> Chain(ModelClass c) {
  var cur = c;
  var guard = new HashSet<string>();
  while (cur != null && guard.Add(cur.Name)) {
    yield return cur;
    cur = cur.Bases.Select(b => classes.GetValueOrDefault(b)).FirstOrDefault(b => b != null);
  }
}
bool WireVisible(ModelClass c) => Chain(c).Any(l => l.Table != null || l.Bases.Any(b => wireBases.Contains(b)));

// flags-wire rule applies to every wire-visible class (stored or transient)
foreach (var c in classes.Values) {
  if (c.FlagLeaks.Count == 0 || !WireVisible(c)) { continue; }
  foreach (var (name, en, missing, line) in c.FlagLeaks)
    findings.Add($"[flags-wire] `{c.Name}.{name}` ({en}) missing {missing} — flags enums must not cross the GraphQL wire; use a numeric *Val mirror ({Rel(c.File)}:{line})");
}

// effective (inherited + auto) indexes per concrete [Table] entity
var entities = new Dictionary<string, EffectiveModel>();
foreach (var c in classes.Values.Where(c => c.Table != null && !c.IsAbstract)) {
  var eff = new EffectiveModel();
  foreach (var link in Chain(c)) {
    foreach (var ix in link.Indexes) { eff.Indexes.Add(ix); }
    foreach (var p in link.Props) { eff.Props.TryAdd(p.Key, p.Value); }
    foreach (var b in link.Bases) {
      if (b is "ICreatedAt" or "IUpdatedAt" or "ITimeStampData") eff.TimeStamped = true;
      if (b is "ModelId" or "ModelNumber") eff.Indexes.Add(new List<string> { "Id" });
    }
    foreach (var bp in link.BoolProps)
      findings.Add($"[bool-column] `{c.Name}.{bp.name}` is a persisted bool — store it as a bit in the [Flags] enum column ({Rel(link.File)}:{bp.line})");
  }
  if (eff.Props.ContainsKey("Id")) eff.Indexes.Add(new List<string> { "Id" });
  if (eff.TimeStamped || eff.Props.ContainsKey("CreatedAt")) eff.Indexes.Add(new List<string> { "CreatedAt" });
  if (eff.TimeStamped || eff.Props.ContainsKey("UpdatedAt")) eff.Indexes.Add(new List<string> { "UpdatedAt" });
  foreach (string p in eff.Props.Keys.Where(p => p.EndsWith("Id") && p.Length > 2)) { eff.Indexes.Add(new List<string> { p }); }
  entities[c.Name] = eff;

  foreach (var ix in c.Indexes.Where(ix => ix.Any(col => eff.Props.TryGetValue(col, out var pt) && flagsEnums.ContainsKey(pt.TrimEnd('?')))))
    findings.Add($"[flags-index] `{c.Name}` declares [ApiIndex({string.Join(", ", ix)})] over a flags column — bitwise predicates can't use it ({Rel(c.File)})");
}

// ---- pass 3: query sites ----
foreach (string file in files) {
  string src = File.ReadAllText(file);
  foreach (Match qm in queryForRx.Matches(src)) {
    string entity = qm.Groups[1].Value;
    if (!entities.TryGetValue(entity, out var eff)) { continue; }
    int end = src.IndexOf(';', qm.Index);
    if (end < 0) { end = Math.Min(src.Length, qm.Index + 800); }
    string chain = src[qm.Index..end];
    int lineNo = src[..qm.Index].Count(chr => chr == '\n') + 1;

    var filterCols = new HashSet<string>();
    foreach (Match fm in filterLambdaRx.Matches(chain)) {
      string p = Regex.Escape(fm.Groups[2].Value);
      foreach (Match cm in Regex.Matches(chain[fm.Index..], @"\b" + p + @"\.(\w+)")) {
        string col = cm.Groups[1].Value;
        if (!eff.Props.TryGetValue(col, out var pType)) { continue; }
        string bare = pType.TrimEnd('?');
        if (classes.ContainsKey(bare) || navTypeRx.IsMatch(pType)) { continue; }   // navigation → join, not a column
        filterCols.Add(col);
      }
    }
    string? sortCol = sortRx.Match(chain) is { Success: true } sm ? sm.Groups[3].Value : null;
    foreach (Match km in filterKeyInRx.Matches(chain)) { filterCols.Add(km.Groups[1].Success ? km.Groups[1].Value : km.Groups[2].Value); }

    if (filterCols.Count == 0 && sortCol == null) { continue; }
    bool covered = eff.Indexes.Any(ix => ix.Count > 0 &&
      (filterCols.Contains(ix[0]) || (filterCols.Count == 0 && ix[0] == sortCol)));
    if (!covered) {
      string what = filterCols.Count > 0 ? $"filter [{string.Join(", ", filterCols)}]" : $"sort [{sortCol}]";
      findings.Add($"[no-index] `{entity}` query {what} has no index leading with any of these columns — add [ApiIndex] (equality cols first, range/sort last) or justify per Docs/data-design.md §2 ({Rel(file)}:{lineNo})");
    }
  }
}

// ---- report ----
// Known debt lives in <root>/.claude/IndexAudit.baseline (one finding per line, `#` comments,
// location suffix optional) so the audit stays green until NEW findings appear.
var baseline = new List<string>();
string baselinePath = Path.Combine(Path.GetFullPath(roots[0]), ".claude", "IndexAudit.baseline");
if (File.Exists(baselinePath)) {
  baseline = File.ReadAllLines(baselinePath).Select(l => l.Trim()).Where(l => l.Length > 0 && !l.StartsWith("#")).Select(StripLoc).ToList();
}
static string StripLoc(string f) {                                   // drop only a trailing "(path[:line])"
  if (!f.EndsWith(")")) return f;
  int i = f.LastIndexOf(" (");
  if (i <= 0) return f;
  string inner = f[(i + 2)..^1];
  return inner.Contains('/') || inner.Contains('\\') ? f[..i] : f;
}

var ordered = findings.Distinct().OrderBy(f => f).ToList();
var fresh = ordered.Where(f => !baseline.Contains(StripLoc(f))).ToList();
int baselined = ordered.Count - fresh.Count;
const string Outro = "Findings are heuristic: verify against real query traffic (Datadog APM `<app>-mysql` service / DBM) before adding or removing an index. Genuinely-accepted findings go in .claude/IndexAudit.baseline with a justifying comment.";
if (hookMode) {                                                      // PostToolUse: silent unless NEW findings
  if (fresh.Count == 0) return 0;
  Console.Error.WriteLine($"index-audit: the edit introduced query/index surface and the repo-wide audit now has {fresh.Count} new finding(s):");
  foreach (string f in fresh) { Console.Error.WriteLine("  " + f); }
  Console.Error.WriteLine(Outro);
  return 2;
}
Console.WriteLine($"IndexAudit: {files.Count} files, {entities.Count} [Table] entities, {flagsEnums.Count} [Flags] enums scanned; {baselined} known finding(s) baselined.");
if (fresh.Count == 0) {
  Console.WriteLine("No new findings.");
  return 0;
}
Console.WriteLine($"{fresh.Count} new finding(s):");
foreach (string f in fresh) { Console.WriteLine("  " + f); }
Console.WriteLine(Outro);
return strict ? 1 : 0;

string Rel(string p) {
  foreach (string r in roots) {
    string full = Path.GetFullPath(r);
    if (p.StartsWith(full)) return p[full.Length..].TrimStart('/');
  }
  return p;
}

class ModelClass {
  public string Name = "", File = "";
  public bool IsAbstract;
  public string? Table;
  public List<string> Bases = new();
  public List<List<string>> Indexes = new();
  public Dictionary<string, string> Props = new();
  public List<(string name, int line)> BoolProps = new();
  public List<(string name, string enumName, string missing, int line)> FlagLeaks = new();
}

class EffectiveModel {
  public bool TimeStamped;
  public List<List<string>> Indexes = new();
  public Dictionary<string, string> Props = new();
}

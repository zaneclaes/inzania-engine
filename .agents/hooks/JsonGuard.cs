#:project ../../ZCore/ZCore.csproj
// inzania-engine JsonGuard — reusable Claude Code PreToolUse hook (Write|Edit|MultiEdit).
// .NET 10 file-based app. Installed by ci/install.cs from ci/claude-hooks.json:
//   dotnet run "$CLAUDE_PROJECT_DIR/inzania-engine/.claude/hooks/JsonGuard.cs"
//   dotnet run inzania-engine/.claude/hooks/JsonGuard.cs -- --audit [dir]   every violation under dir, exit 1 if any
// Reads the hook JSON from stdin. Exit 0 = allow, exit 2 = BLOCK (reason on stderr).
//
// THE RULE: `ZJson` (IZ.Core.Json.ZJson) is the only way code reads or writes JSON. One serializer means one
// naming policy, one enum wire format (ZEnums), one null policy and one set of converters on every surface —
// server, WebAssembly, Unity, tests and scripts. A second serializer, or JSON assembled by hand, drifts from it
// silently: a key cased differently, an enum spelled differently, a quote left unescaped.
//
// BLOCK rules, applied to what an edit ADDS (violations already in the file are subtracted, so older code
// is not blocked until someone writes more of it):
//  J1  a JSON API other than ZJson: System.Text.Json (JsonSerializer, JsonDocument/JsonElement, JsonNode and
//      its DOM, Utf8JsonWriter/Reader, JsonSerializerOptions, custom JsonConverters), Newtonsoft (JsonConvert,
//      JObject…), Unity's JsonUtility, DataContractJsonSerializer, HttpClient's *AsJsonAsync helpers,
//      JsonContent, Results.Json/JsonResult. The serialization ATTRIBUTES ([JsonIgnore], [JsonPropertyName],
//      [JsonPolymorphic]…) are allowed: ZJson reads them.
//  J2  JSON assembled by hand in a string: a quoted key followed by a colon (`"name": `, `\"@type\":`), or a
//      literal that opens like an object or array of them.
// The fix is always the same: a typed object (a DTO; [JsonPropertyName] for a wire name ZJson's camelCase would
// not produce) through ZJson.SerializeObject / ZJson.DeserializeObject<T>. A file-based script references ZCore
// (`#:project <engine>/ZCore/ZCore.csproj`) and calls `ZScriptApp.Start(...)` first, as this file does.
// Justified exception (a byte-exact third-party wire sample, say): `// json-guard: allow` on the line or the
// line above, with the reason.
//
// Exempt paths: ZJson's own implementation (ZCore/Json/), generated code, vendored plugins, build output, and
// this file. .razor/.cshtml get J1 only (markup quotes are not C# strings).
using System.Text;
using System.Text.RegularExpressions;
using IZ.Core.Contexts;
using IZ.Core.Tooling;

ZScriptApp.Start("JsonGuard");

var exempt = new Regex(@"/ZCore/Json/|/Types/|/User/GraphQL/|/Generated/|/Plugins/|/Library/|/PackageCache/|/obj/|/bin/|/out/|/node_modules/|TuneQuery\.Client\.cs$|/\.claude/hooks/JsonGuard\.cs$");
var apiRx = new Regex(
  @"\b(JsonSerializer|JsonSerializerOptions|JsonDocument|JsonElement|JsonNode|JsonObject|JsonArray|JsonValue|JsonConverter|JsonConverterFactory|Utf8JsonWriter|Utf8JsonReader|JsonConvert|JObject|JArray|JToken|JValue|JsonUtility|EditorJsonUtility|DataContractJsonSerializer|JavaScriptSerializer|JsonContent|JsonResult)\b" +
  @"|\b(ReadFromJsonAsync|GetFromJsonAsync|PostAsJsonAsync|PutAsJsonAsync|PatchAsJsonAsync|DeleteFromJsonAsync)\b" +
  @"|\bResults\s*\.\s*Json\b" +
  @"|\busing\s+(?:static\s+)?(?:System\.Text\.Json(?:\.Nodes)?|Newtonsoft\.Json[\w.]*|System\.Net\.Http\.Json)\s*;");
var pairRx = new Regex(@"""[@$A-Za-z_][\w@$.\-]*""\s*:\s*[""\d\[{tfn\-]|^\s*[\[{]\s*""[@$A-Za-z_]");

const string J1 = "J1 a JSON API other than ZJson";
const string J2 = "J2 JSON assembled by hand in a string";
const string Fix = "Only ZJson reads or writes JSON: use a typed object with ZJson.SerializeObject / ZJson.DeserializeObject<T> ([JsonPropertyName] for a wire name camelCase would not produce). A file-based script: `#:project <engine>/ZCore/ZCore.csproj` and `ZScriptApp.Start(...)` first. A justified exception: `// json-guard: allow` on the line, with the reason.";

var cli = Environment.GetCommandLineArgs().Skip(1).ToArray();
if (cli.Contains("--audit")) return Audit(cli.SkipWhile(a => a != "--audit").Skip(1).FirstOrDefault() ?? ".");

var hook = ClaudeHookInput.Read(Console.In.ReadToEnd());
var ti = hook?.ToolInput;
string path = (ti?.FilePath ?? "").Replace('\\', '/');
if (ti == null || !ti.WritesText || !IsJsonCandidate(path) || exempt.IsMatch(path)) return 0;

string existing = "";
try { if (File.Exists(path)) existing = File.ReadAllText(path); } catch { }
string content = ti.ProspectiveContent(existing);

bool razor = !path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase);
var before = Scan(existing, razor).GroupBy(v => v.Key).ToDictionary(g => g.Key, g => g.Count());
var added = new List<Violation>();
foreach (var v in Scan(content, razor)) {
  if (before.TryGetValue(v.Key, out int n) && n > 0) { before[v.Key] = n - 1; continue; }
  added.Add(v);
}
if (added.Count <= 0) return 0;

Console.Error.WriteLine($"json-guard BLOCKED {(hook!.ToolName is { Length: > 0 } t ? t : "edit")} to {path}");
foreach (var v in added.Take(12)) Console.Error.WriteLine($"  - line {v.Line}: {v.Rule}: {v.Snippet}");
if (added.Count > 12) Console.Error.WriteLine($"  - … and {added.Count - 12} more");
Console.Error.WriteLine("  " + Fix);
return 2;

int Audit(string dir) {
  int count = 0;
  // Symlinked directories are skipped: Unity's Assets/ links into the shared projects, which would otherwise be
  // reported twice, once under a path git does not know.
  var walk = new EnumerationOptions { RecurseSubdirectories = true, AttributesToSkip = FileAttributes.ReparsePoint };
  foreach (var file in Directory.EnumerateFiles(dir, "*.*", walk)) {
    string p = Path.GetFullPath(file).Replace('\\', '/');
    if (!IsJsonCandidate(p) || exempt.IsMatch(p) || p.Contains("/.git/") || p.Contains("/.claude/worktrees/")) continue;
    string text;
    try { text = File.ReadAllText(file); } catch { continue; }
    foreach (var v in Scan(text, !p.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))) {
      Console.WriteLine($"{Path.GetRelativePath(dir, file)}:{v.Line}: {v.Rule}: {v.Snippet}");
      count++;
    }
  }
  Console.Error.WriteLine(count > 0 ? $"json-guard audit: {count} violation(s)" : "json-guard audit: clean");
  return count > 0 ? 1 : 0;
}

static bool IsJsonCandidate(string p) =>
  p.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) || p.EndsWith(".razor", StringComparison.OrdinalIgnoreCase) ||
  p.EndsWith(".cshtml", StringComparison.OrdinalIgnoreCase);

List<Violation> Scan(string text, bool markup) {
  var found = new List<Violation>();
  if (text.Length <= 0) return found;
  string[] lines = text.Split('\n');
  string code = markup ? text : Blank(text, out _);
  foreach (Match m in apiRx.Matches(code)) Add(found, lines, text, m.Index, J1);
  if (!markup) {
    Blank(text, out var literals);
    foreach (var (start, value) in literals)
      if (pairRx.IsMatch(value)) Add(found, lines, text, start, J2);
  }
  return found;
}

void Add(List<Violation> found, string[] lines, string text, int index, string rule) {
  int line = 1;
  for (int i = 0; i < index && i < text.Length; i++) if (text[i] == '\n') line++;
  string raw = lines[line - 1];
  string above = line >= 2 ? lines[line - 2] : "";
  if (raw.Contains("json-guard: allow") || above.Contains("json-guard: allow")) return;
  string snippet = raw.Trim();
  found.Add(new Violation(line, rule, snippet.Length > 120 ? snippet[..120] + "…" : snippet, rule + "|" + snippet));
}

// Comments and string contents blanked (newlines kept, offsets aligned) so API names in prose or in strings do not
// count; the literals themselves are returned with their offsets, unescaped enough to see the JSON inside them.
static string Blank(string s, out List<(int Start, string Value)> literals) {
  literals = new List<(int, string)>();
  var sb = new StringBuilder(s);
  int i = 0;
  while (i < s.Length) {
    char ch = s[i];
    if (ch == '/' && i + 1 < s.Length && s[i + 1] == '/') {
      while (i < s.Length && s[i] != '\n') { sb[i] = ' '; i++; }
    } else if (ch == '/' && i + 1 < s.Length && s[i + 1] == '*') {
      while (i < s.Length && !(s[i] == '*' && i + 1 < s.Length && s[i + 1] == '/')) { if (s[i] != '\n') sb[i] = ' '; i++; }
      if (i + 1 < s.Length) { sb[i] = ' '; sb[i + 1] = ' '; i += 2; }
    } else if (ch == '"') {
      int start = i;
      int prefix = start;
      while (prefix > 0 && (s[prefix - 1] == '$' || s[prefix - 1] == '@')) prefix--;
      bool verbatim = s[prefix..start].Contains('@');
      bool interpolated = s[prefix..start].Contains('$');
      int quotes = 0;
      while (i + quotes < s.Length && s[i + quotes] == '"') quotes++;
      var value = new StringBuilder();
      if (quotes >= 3) {                                                    // raw string literal
        i += quotes;
        while (i < s.Length && !(i + quotes <= s.Length && s.Substring(i, quotes) == new string('"', quotes))) {
          value.Append(s[i]); if (s[i] != '\n') sb[i] = ' '; i++;
        }
        i += quotes;
      } else {
        i++;
        while (i < s.Length) {
          if (verbatim && s[i] == '"' && i + 1 < s.Length && s[i + 1] == '"') { value.Append('"'); sb[i] = ' '; sb[i + 1] = ' '; i += 2; continue; }
          if (s[i] == '"') break;
          if (!verbatim && s[i] == '\\' && i + 1 < s.Length) { value.Append(s[i + 1] == 'n' ? '\n' : s[i + 1]); sb[i] = ' '; sb[i + 1] = ' '; i += 2; continue; }
          value.Append(s[i]); if (s[i] != '\n') sb[i] = ' ';
          i++;
        }
        i++;
      }
      string v = value.ToString();
      if (interpolated) v = v.Replace("{{", "{").Replace("}}", "}");
      literals.Add((start, v));
    } else if (ch == '\'') {
      i++;
      while (i < s.Length && s[i] != '\'' && s[i] != '\n') { if (s[i] == '\\') { sb[i] = ' '; i++; } if (i < s.Length) { sb[i] = ' '; i++; } }
      i++;
    } else { i++; }
  }
  return sb.ToString();
}

record Violation(int Line, string Rule, string Snippet, string Key);

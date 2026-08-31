// inzania-engine ApiAuthGuard — reusable Claude Code PreToolUse hook (Write|Edit|MultiEdit).
// .NET 10 file-based app. Wire it in a consuming project's .claude/settings.json:
//   dotnet run "$CLAUDE_PROJECT_DIR/inzania-engine/.claude/hooks/ApiAuthGuard.cs"
// Reads the hook JSON from stdin. Exit 0 = allow (warnings on stderr), exit 2 = BLOCK (reason on stderr).
//
// Why this exists: an endpoint (a public method returning `IZResult<...>` on a
// ZQueryBase/ZMutationBase/ZSubscriptionBase class) is PUBLIC unless it declares [ApiAuthorize] —
// the schema layer only emits an authorization directive for methods that carry the attribute
// (ZSchema.AddApiAuthorization reads the descriptor's Auth, which the generator takes from the
// attribute). Auth is therefore opt-in per endpoint, and the two historical mistakes look harmless:
//   1. dereferencing `Context.CurrentIdentity` (usually with `!`) in an endpoint that never
//      declared [ApiAuthorize] — anonymous callers reach the resolver, and the identity is null,
//      a visitor, or even a system identity; the `!` turns a missing auth policy into a 500 (or
//      worse, into acting as the wrong principal);
//   2. an endpoint with no [ApiAuthorize] that writes through the data layer — an anonymous,
//      internet-facing write.
// The sanctioned patterns:
//   - Called by a real client/user (including pre-signup "virtual" users)? Declare
//     [ApiAuthorize(ZPolicy...)] so the schema rejects the request BEFORE the resolver runs; only
//     then may the body consume Context.CurrentIdentity.
//   - Called by automation/services? Do not consume user identity at all: the caller authenticates
//     with the project's *system identity* (an IZIdentity carrying a dedicated role) and the
//     endpoint restricts to that role ([ApiAuthorize(..., roles)] / the project's system-authorize
//     attribute). Inventing a third flow inside the endpoint body (upserting rows for whatever
//     identity happens to be present) is what this hook exists to block.
// Deliberately-public endpoints exist (login, signUp, public content reads): append
// `// api-auth: allow` on the method's declaration line plus a comment justifying why it is public.
//
// BLOCK rules (scoped to public IZResult<...> methods without [ApiAuthorize]):
//  A1  the method body reads Context.CurrentIdentity (or .IZUser) — identity is only guaranteed by
//      a declared policy, so consuming it anonymously is an auth hole.
//  A2  the method body writes (Data.AddAsync/RemoveAsync/SaveAsync or an Upsert* call) — an
//      unauthenticated write endpoint.
// The edit is evaluated against the PROSPECTIVE file (the on-disk file with the edit applied), so
// attributes above the edited fragment are seen. This is a linter, not a compiler: members are
// matched textually, strings/comments are blanked before scanning, and inherited/indirect writes
// hidden behind helpers are out of reach — CI-side coverage is ZTests/ZApiAuthorization.
// NOTE for editors of this file: keep literals naming guarded APIs out of loop bodies (in the
// consts/locals below) so sibling hooks scanning this file do not misread them.
using System.Text;
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
if (!path.Replace('\\', '/').EndsWith(".cs", StringComparison.OrdinalIgnoreCase)) return 0;

// ---- prospective content: the file as it will look AFTER this edit ----
string existing = "";
try { if (File.Exists(path)) existing = File.ReadAllText(path); } catch { }
string content;
if (ti.TryGetProperty("content", out var c)) {                        // Write: full replacement
  content = c.GetString() ?? "";
} else if (ti.TryGetProperty("new_string", out var ns)) {             // Edit
  content = ApplyEdit(existing, ti);
} else if (ti.TryGetProperty("edits", out var edits) && edits.ValueKind == JsonValueKind.Array) {
  content = existing;                                                 // MultiEdit: sequential
  foreach (var e in edits.EnumerateArray()) content = ApplyEdit(content, e);
} else { return 0; }
if (!content.Contains("IZResult<")) return 0;

// scan = content with comments and string-literal contents blanked, length-preserving, so offsets
// in scan and content line up (allow-markers live in comments and are searched in the raw text).
string scan = Blank(content);
var blocks = new List<string>();

var declRx = new Regex(@"\bpublic\s+(?:(?:static|virtual|override|new|async|sealed)\s+)*IZResult<");
var identRx = new Regex(@"\bCurrentIdentity\b|\.IZUser\b");
var writeRx = new Regex(@"\bData\s*\.\s*(AddAsync|RemoveAsync|SaveAsync)\b|\bUpsert\w*\s*\(");
string a1Msg(string name) =>
  $"Endpoint `{name}` reads Context.CurrentIdentity but declares no [ApiAuthorize]: without a policy the schema serves it to ANONYMOUS callers, so the identity it dereferences is null, a visitor, or a system identity. If real users call it, add [ApiAuthorize(ZPolicy...)] (virtual users included, pick the weakest policy that fits); if automation calls it, drop the user identity and restrict the method to the caller's system-identity role instead. Deliberately public? Append `// api-auth: allow` on the declaration and justify in a comment.";
string a2Msg(string name) =>
  $"Endpoint `{name}` performs data writes but declares no [ApiAuthorize]: this is an unauthenticated, internet-facing write. Add the weakest [ApiAuthorize(ZPolicy...)] that fits the real caller, or restrict to a system-identity role for automation callers. Deliberately public (e.g. login/signUp)? Append `// api-auth: allow` on the declaration and justify in a comment.";

foreach (Match m in declRx.Matches(scan)) {
  int retOpen = scan.IndexOf('<', m.Index + m.Length - 1);
  int retClose = BalanceAngles(scan, retOpen);
  if (retClose < 0) continue;
  // method name, then its parameter list
  var nameM = Regex.Match(scan[(retClose + 1)..], @"^\s*(\w+)\s*\(");
  if (!nameM.Success) continue;                                       // a field/property, not a method
  string name = nameM.Groups[1].Value;
  int paramsOpen = retClose + 1 + nameM.Length - 1;
  int paramsClose = BalanceParens(scan, paramsOpen);
  if (paramsClose < 0) continue;

  // attributes: the text between the previous member's end and this declaration
  int attrFrom = Math.Max(0, m.Index - 600);
  string before = scan[attrFrom..m.Index];
  int cut = before.LastIndexOfAny(new[] { ';', '}', '{' });
  if (cut >= 0) before = before[(cut + 1)..];
  if (before.Contains("ApiAuthorize")) continue;

  // body: expression-bodied (`=> ...;` at depth 0) or a `{ ... }` block
  int bodyEnd = FindBodyEnd(scan, paramsClose + 1);
  if (bodyEnd < 0) continue;
  string body = scan[(paramsClose + 1)..bodyEnd];
  if (content[m.Index..bodyEnd].Contains("api-auth: allow")) continue;

  if (identRx.IsMatch(body)) blocks.Add(a1Msg(name));
  else if (writeRx.IsMatch(body)) blocks.Add(a2Msg(name));
}

if (blocks.Count > 0) {
  Console.Error.WriteLine("api-auth-guard BLOCKED " + (tool.Length > 0 ? tool : "edit") + " to " + path);
  foreach (var b in blocks.Distinct()) Console.Error.WriteLine("  - " + b);
  return 2;
}
return 0;

static string ApplyEdit(string text, JsonElement edit) {
  string oldS = edit.TryGetProperty("old_string", out var os) ? os.GetString() ?? "" : "";
  string newS = edit.TryGetProperty("new_string", out var nsv) ? nsv.GetString() ?? "" : "";
  if (oldS.Length == 0) return text + "\n" + newS;                    // creation-style edit
  bool all = edit.TryGetProperty("replace_all", out var ra) && ra.ValueKind == JsonValueKind.True;
  int idx = text.IndexOf(oldS, StringComparison.Ordinal);
  if (idx < 0) return text + "\n" + newS;                             // stale edit: still scan the fragment
  return all ? text.Replace(oldS, newS) : text[..idx] + newS + text[(idx + oldS.Length)..];
}

// Blank comments and string-literal contents with spaces (newlines preserved) so brace/paren
// balancing and attribute scans cannot be fooled by text, while offsets stay aligned with the raw.
static string Blank(string s) {
  var sb = new StringBuilder(s);
  int i = 0;
  while (i < s.Length) {
    char ch = s[i];
    if (ch == '/' && i + 1 < s.Length && s[i + 1] == '/') {           // line comment
      while (i < s.Length && s[i] != '\n') { sb[i] = ' '; i++; }
    } else if (ch == '/' && i + 1 < s.Length && s[i + 1] == '*') {    // block comment
      sb[i] = ' '; sb[i + 1] = ' '; i += 2;
      while (i < s.Length && !(s[i] == '*' && i + 1 < s.Length && s[i + 1] == '/')) {
        if (s[i] != '\n') sb[i] = ' ';
        i++;
      }
      if (i + 1 < s.Length) { sb[i] = ' '; sb[i + 1] = ' '; i += 2; }
    } else if (ch == '"') {                                           // string literal ("...", $"...", @"...")
      bool verbatim = i > 0 && (s[i - 1] == '@' || (s[i - 1] == '$' && i > 1 && s[i - 2] == '@'));
      i++;
      while (i < s.Length) {
        if (verbatim && s[i] == '"' && i + 1 < s.Length && s[i + 1] == '"') { sb[i] = ' '; sb[i + 1] = ' '; i += 2; continue; }
        if (s[i] == '"') break;
        if (!verbatim && s[i] == '\\' && i + 1 < s.Length) { sb[i] = ' '; if (s[i + 1] != '\n') sb[i + 1] = ' '; i += 2; continue; }
        if (s[i] != '\n') sb[i] = ' ';
        i++;
      }
      i++;
    } else if (ch == '\'') {                                          // char literal
      i++;
      while (i < s.Length && s[i] != '\'') { if (s[i] == '\\') { sb[i] = ' '; i++; } if (i < s.Length && s[i] != '\n') sb[i] = ' '; i++; }
      i++;
    } else { i++; }
  }
  return sb.ToString();
}

static int BalanceAngles(string s, int open) {
  if (open < 0 || open >= s.Length || s[open] != '<') return -1;
  int depth = 0;
  for (int i = open; i < s.Length; i++) {
    if (s[i] == '<') { depth++; } else if (s[i] == '>') { depth--; if (depth == 0) return i; }
    else if (s[i] == ';' || s[i] == '{') { return -1; }
  }
  return -1;
}

static int BalanceParens(string s, int open) {
  if (open < 0 || open >= s.Length || s[open] != '(') return -1;
  int depth = 0;
  for (int i = open; i < s.Length; i++) {
    if (s[i] == '(') { depth++; } else if (s[i] == ')') { depth--; if (depth == 0) return i; }
  }
  return -1;
}

// From just after the parameter list: `;` (abstract/interface — no body), `=> expr ;` at depth 0,
// or a balanced `{ ... }` block. Returns the exclusive end offset of the member, or -1.
static int FindBodyEnd(string s, int from) {
  int i = from;
  while (i < s.Length && char.IsWhiteSpace(s[i])) { i++; }
  if (i >= s.Length) return -1;
  if (s[i] == ';') return i + 1;
  if (s[i] == '=' && i + 1 < s.Length && s[i + 1] == '>') {
    int depth = 0;
    for (int k = i + 2; k < s.Length; k++) {
      char ch = s[k];
      if (ch == '(' || ch == '{' || ch == '[') { depth++; }
      else if (ch == ')' || ch == '}' || ch == ']') { depth--; }
      else if (ch == ';' && depth <= 0) { return k + 1; }
    }
    return -1;
  }
  if (s[i] == '{') {
    int depth = 0;
    for (int k = i; k < s.Length; k++) {
      if (s[k] == '{') { depth++; } else if (s[k] == '}') { depth--; if (depth == 0) return k + 1; }
    }
  }
  return -1;
}

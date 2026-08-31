// inzania-engine ApiRegistrationGuard — every ZQueryBase/ZMutationBase/ZSubscriptionBase subclass
// must be reachable from a registration class (.NET 10 file-based app).
//   dotnet run inzania-engine/.claude/hooks/ApiRegistrationGuard.cs -- <repo-root> [--strict]
//   dotnet run inzania-engine/.claude/hooks/ApiRegistrationGuard.cs -- --hook   (Claude Code PostToolUse)
//
// Why this exists: an API class is invoked TWO ways, and only one of them is reflective.
//  1. Over the GraphQL wire, HotChocolate resolves it from the reflected method map
//     (ZApiTypeGenerator.CacheApiMethods -> ZSchema.AddZRequestDescriptors).
//  2. **In C#, through `Context.BeginRequest<TReq>()`** — the request tree every non-GraphQL caller
//     uses (Chordzy: `Context.BeginRequest<AuthQuery>().CurrentUser().Execute(...)` from the client,
//     caches, controllers and tests). That tree is the aggregate registration class:
//     TuneRequest -> TuneQuery/TuneMutation/TuneSubscription -> each `*Query`/`*Mutation`.
// A class the aggregate never names is unreachable through (2): nothing fails to compile and the
// class looks finished, but no caller can get to it, and on a stripped client build the reflective
// path of (1) cannot save it either — the aggregate's public property is the only static reference
// keeping the type alive. Registering it is one line.
// The registration class name differs per project, so this guard never hard-codes it: the only
// fixed names are the three engine base classes. A registration class is discovered structurally —
// any class that is not itself an API class and declares a public property (or field) typed as one.
// A project with NO registration class at all has not built its request tree yet: that is the
// finding, not a reason to skip — every API class in it is unreachable through BeginRequest.
//
// Advisory by default (exit 0); --strict exits 1 when findings exist.
// --hook mode: reads PostToolUse JSON from stdin, exits 0 unless the edited file is .cs AND either
// side of the edit mentions an API base class / IZResult (a new or removed endpoint) or the file
// looks like a registration class. Findings go to stderr with exit 2 so the model sees them.
//
// Known approximations (this is a linter, not a compiler):
//  - members are associated to the nearest preceding class declaration in the file;
//  - inheritance is resolved by simple name across the whole repo (namespaces are ignored);
//  - a class with no `IZResult<...>` member is treated as a base/helper and is not required to be
//    registered (only classes that actually expose endpoints are).
using System.Text.Json;
using System.Text.RegularExpressions;

var roots = new List<string>();
bool strict = false, hookMode = false;
foreach (string a in args) {
  if (a == "--strict") { strict = true; } else if (a == "--hook") { hookMode = true; } else { roots.Add(a); }
}

string[] apiBases = { "ZQueryBase", "ZMutationBase", "ZSubscriptionBase" };

// Gate: an edit matters when it declares/removes an API class or endpoint, or touches a file whose
// name reads like a registration aggregate (Query/Mutation/Subscription with no other suffix).
var gateRx = new Regex(@"ZQueryBase|ZMutationBase|ZSubscriptionBase|IZResult<");
var registrationFileRx = new Regex(@"(Query|Mutation|Subscription|Request)s?\.cs$", RegexOptions.IgnoreCase);
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
  bool touchesApi = gateRx.IsMatch(newText) || gateRx.IsMatch(oldText);
  if (!touchesApi && !registrationFileRx.IsMatch(hookPath)) return 0;
  string projectDir = Environment.GetEnvironmentVariable("CLAUDE_PROJECT_DIR") ?? Directory.GetCurrentDirectory();
  roots.Clear();
  roots.Add(projectDir);
}
if (roots.Count == 0) roots.Add(Directory.GetCurrentDirectory());

string[] skipDirs = { "obj", "bin", "out", "node_modules", ".git", "Temp", "Library", "Logs", "Migrations", "Generated", "PackageCache" };

var classRx = new Regex(@"(?:(?:public|internal|private|protected|abstract|sealed|partial|static)[ \t]+)*class[ \t]+(\w+)(?:[ \t]*<[^>]*>)?(?:[ \t]*:[ \t]*([^{\r\n]+))?");
// Registration is a public member typed as an API class: `public X Foo => ...`, `public X Foo { get`,
// or a plain `public X Foo;` field.
var publicMemberRx = new Regex(@"public[ \t]+(\w+)\??[ \t]+(\w+)[ \t]*(?:=>|\{|;|=)");
var resultRx = new Regex(@"IZResult<");

var files = new List<string>();
foreach (string root in roots) {
  if (!Directory.Exists(root)) { Console.Error.WriteLine($"ApiRegistrationGuard: no such directory {root}"); return 1; }
  foreach (string f in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)) {
    var parts = f.Split(Path.DirectorySeparatorChar);
    if (parts.Any(p => skipDirs.Contains(p))) continue;
    files.Add(f);
  }
}

// ---- pass 1: class graph ----
var baseNames = new Dictionary<string, List<string>>();     // class -> declared bases
var isAbstract = new HashSet<string>();
var declaredIn = new Dictionary<string, string>();          // class -> file
var hasEndpoint = new HashSet<string>();                    // class -> declares an IZResult<...> member
var fileText = new Dictionary<string, string>();

foreach (string f in files) {
  string text;
  try { text = File.ReadAllText(f); } catch { continue; }
  if (!text.Contains("class ")) continue;
  fileText[f] = text;
  foreach (Match m in classRx.Matches(text)) {
    string name = m.Groups[1].Value;
    var bases = m.Groups[2].Success
      ? m.Groups[2].Value.Split(',').Select(b => b.Trim().Split('<')[0].Trim()).Where(b => b.Length > 0).ToList()
      : new List<string>();
    if (!baseNames.ContainsKey(name)) {
      baseNames[name] = bases;
      declaredIn[name] = f;
    } else {
      baseNames[name].AddRange(bases.Where(b => !baseNames[name].Contains(b)));
    }
    if (m.Value.Contains("abstract")) isAbstract.Add(name);
    // Endpoint detection: scan the body until the next class declaration in the same file.
    int start = m.Index + m.Length;
    var next = classRx.Match(text, start);
    int end = next.Success ? next.Index : text.Length;
    if (resultRx.IsMatch(text.Substring(start, end - start))) hasEndpoint.Add(name);
  }
}

bool DerivesFrom(string cls, string target) {
  var seen = new HashSet<string>();
  var stack = new Stack<string>();
  stack.Push(cls);
  while (stack.Count > 0) {
    string c = stack.Pop();
    if (!seen.Add(c)) continue;
    if (c == target) return cls != target;
    if (!baseNames.TryGetValue(c, out var bs)) continue;
    foreach (string b in bs) stack.Push(b);
  }
  return false;
}

// ---- pass 2: the API classes, grouped by which engine base they serve ----
var apiClasses = new Dictionary<string, string>();  // class -> base kind
foreach (string cls in baseNames.Keys) {
  if (apiBases.Contains(cls) || isAbstract.Contains(cls)) continue;
  foreach (string b in apiBases) {
    if (DerivesFrom(cls, b)) { apiClasses[cls] = b; break; }
  }
}

// ---- pass 3: registration classes and what they expose ----
var registered = new Dictionary<string, List<string>>();   // api class -> registration classes
foreach (var (f, text) in fileText) {
  foreach (Match m in classRx.Matches(text)) {
    string owner = m.Groups[1].Value;
    if (apiClasses.ContainsKey(owner)) continue;   // an API class holding another is not a registry
    int start = m.Index + m.Length;
    var next = classRx.Match(text, start);
    int end = next.Success ? next.Index : text.Length;
    foreach (Match p in publicMemberRx.Matches(text.Substring(start, end - start))) {
      string type = p.Groups[1].Value;
      if (!apiClasses.ContainsKey(type)) continue;
      if (!registered.TryGetValue(type, out var owners)) registered[type] = owners = new List<string>();
      if (!owners.Contains(owner)) owners.Add(owner);
    }
  }
}

// ---- report ----
var findings = new List<string>();
foreach (var (cls, kind) in apiClasses.OrderBy(k => k.Key)) {
  if (registered.ContainsKey(cls)) continue;
  if (!hasEndpoint.Contains(cls)) continue;   // no IZResult members: a base/helper, nothing to expose
  string rel = Path.GetRelativePath(roots[0], declaredIn[cls]);
  var peers = apiClasses.Where(k => k.Value == kind && registered.ContainsKey(k.Key)).Select(k => k.Key).ToList();
  string where = peers.Count > 0 && registered.TryGetValue(peers[0], out var ex)
    ? $" — register it beside {peers[0]} in {string.Join('/', ex)}"
    : "";
  findings.Add($"{rel}: {cls} : {kind} is never exposed as a public property of a registration class{where}. " +
               "Nothing in C# can reach its endpoints: `Context.BeginRequest<" + cls + ">()` resolves through the " +
               "request tree, and the tree is built out of those properties. Add one line to the aggregate.");
}

Console.WriteLine($"ApiRegistrationGuard: {files.Count} files, {apiClasses.Count} API class(es), " +
                  $"{registered.Count} registered.");
if (findings.Count == 0) {
  Console.WriteLine("No unregistered API classes.");
  return 0;
}
var w = hookMode ? Console.Error : Console.Out;
w.WriteLine($"ApiRegistrationGuard: {findings.Count} unregistered API class(es):");
foreach (string f in findings) w.WriteLine($"  - {f}");
return hookMode ? 2 : (strict ? 1 : 0);

#!/usr/bin/env dotnet
#:project ../ZCore/ZCore.csproj
// Adapter shared by the Claude Code, Codex and Grok hook declarations (rendered by install.cs). Claude
// already sends the shape the guards consume. Codex sends `apply_patch` as one unified diff, so this
// splits it into one Claude-compatible edit payload per file. Grok sends camelCase `toolName`/`toolInput`
// with its own tool names, so this translates it to Claude's names and fields. Guard logic stays in one file.
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using System.Text.Json.Serialization;
using IZ.Core.Contexts;
using IZ.Core.Json;

ZScriptApp.Start("RunAgentHook");
try {
if (args.FirstOrDefault() is "--prepare-hooks" or "--check-hooks") {
  var entries = new Dictionary<string, string>();
  foreach (var script in args.Skip(1).Distinct(StringComparer.Ordinal)) {
    var executable = HookCache.Resolve(script, args[0] == "--prepare-hooks");
    if (executable == null && !HookCache.Supports(script)) executable = Path.GetFullPath(script);
    if (executable == null) throw new InvalidOperationException($"No current compiled hook: {script}");
    entries.Add(Path.GetFullPath(script), executable);
  }
  Console.WriteLine(ZJson.SerializeObject(entries));
  return 0;
}
int guardAt = Array.IndexOf(args, "--guard");
if (guardAt < 0 || guardAt + 1 >= args.Length) {
  Console.Error.WriteLine("Usage: dotnet run RunAgentHook.cs -- [--runtime <name>] --guard <guard.cs> [guard arguments]");
  return 1;
}

string guard = args[guardAt + 1];
string[] guardArgs = args.Skip(guardAt + 2).ToArray();
int runtimeAt = Array.IndexOf(args, "--runtime");
string? runtime = runtimeAt >= 0 && runtimeAt + 1 < guardAt ? args[runtimeAt + 1] : null;
string stdin = Console.In.ReadToEnd();
// A runtime may retain an older generated command during an active session. That installed
// adapter validates its source/dependencies too, then hands off before interpreting the payload.
string? currentAdapter = HookCache.Resolve(HookCache.SourcePath());
if (currentAdapter != null && Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "RunAgentHook.dll")) != currentAdapter) {
  var forwarded = RunGuard(HookCache.SourcePath(), args, stdin, runtime);
  Console.Error.Write(forwarded.Output);
  return forwarded.Code;
}
foreach (string payload in Payloads(stdin)) {
  var result = RunGuard(guard, guardArgs, payload, runtime);
  if (result.Output.Length > 0) Console.Error.Write(result.Output);
  if (result.Code != 0) return result.Code;
}
return 0;
} catch (Exception e) {
  Console.Error.WriteLine($"AGENT_HOOK_UNAVAILABLE: {e.Message}");
  return 2; // Tooling failure must not silently allow the protected action.
}

static IEnumerable<string> Payloads(string stdin) {
  HookEnvelope? hook = null;
  bool invalid = false;
  try {
    hook = ZJson.DeserializeObject<HookEnvelope>(null, stdin);
  } catch {
    invalid = true;
  }
  if (invalid) {
    yield return stdin;
    yield break;
  }
  if (hook?.ToolName == null && FromGrok(stdin) is { } grok) {
    yield return ZJson.SerializeObject(grok);
    yield break;
  }
  if (hook?.ToolName != "apply_patch" || string.IsNullOrWhiteSpace(hook.ToolInput?.Command)) {
    yield return stdin;
    yield break;
  }

  foreach (PatchFile patch in ParsePatch(hook.ToolInput.Command)) {
    var converted = new HookEnvelope {
      ToolName = "Write",
      ToolInput = new HookToolInput {
        FilePath = patch.Path,
        NewString = patch.Added,
        OldString = patch.Removed,
      },
    };
    yield return ZJson.SerializeObject(converted);
  }
}

// Grok names its tools and fields differently; the guards only understand Claude's. Null when the
// payload is not Grok's (no camelCase `toolName`).
static GuardEnvelope? FromGrok(string stdin) {
  GrokEnvelope? grok;
  try {
    grok = ZJson.DeserializeObject<GrokEnvelope>(null, stdin);
  } catch {
    return null;
  }
  if (string.IsNullOrEmpty(grok?.ToolName)) return null;
  GrokToolInput? input = grok.ToolInput;
  return new GuardEnvelope {
    ToolName = grok.ToolName switch {
      "run_terminal_command" or "run_terminal_cmd" => "Bash",
      "search_replace" => "Edit",
      "write_file" => "Write",
      _ => grok.ToolName,
    },
    ToolInput = new GuardToolInput {
      Command = input?.Command,
      FilePath = input?.FilePath ?? input?.FilePathCamel ?? input?.Path,
      Content = input?.Content,
      OldString = input?.OldString ?? input?.OldStringCamel,
      NewString = input?.NewString ?? input?.NewStringCamel,
      ReplaceAll = input?.ReplaceAll ?? input?.ReplaceAllCamel ?? false,
      Edits = input?.Edits,
    },
  };
}

static IEnumerable<PatchFile> ParsePatch(string patch) {
  string? path = null;
  var added = new List<string>();
  var removed = new List<string>();
  foreach (string line in patch.Replace("\r\n", "\n").Split('\n')) {
    const string add = "*** Add File: ";
    const string update = "*** Update File: ";
    const string delete = "*** Delete File: ";
    if (line.StartsWith(add, StringComparison.Ordinal) || line.StartsWith(update, StringComparison.Ordinal) || line.StartsWith(delete, StringComparison.Ordinal)) {
      if (path != null) yield return new PatchFile(path, string.Join("\n", added), string.Join("\n", removed));
      path = line.Substring(line.IndexOf(": ", StringComparison.Ordinal) + 2).Trim();
      added.Clear();
      removed.Clear();
      continue;
    }
    if (path == null) continue;
    if (line.StartsWith('+') && !line.StartsWith("+++", StringComparison.Ordinal)) added.Add(line.Substring(1));
    if (line.StartsWith('-') && !line.StartsWith("---", StringComparison.Ordinal)) removed.Add(line.Substring(1));
  }
  if (path != null) yield return new PatchFile(path, string.Join("\n", added), string.Join("\n", removed));
}

static (int Code, string Output) RunGuard(string guard, IEnumerable<string> guardArgs, string input, string? runtime) {
  bool csharp = Path.GetExtension(guard).Equals(".cs", StringComparison.OrdinalIgnoreCase);
  string? compiled = csharp ? HookCache.Resolve(guard) : null;
  var start = new ProcessStartInfo(csharp ? "dotnet" : "sh") {
    RedirectStandardInput = true,
    RedirectStandardOutput = true,
    RedirectStandardError = true,
  };
  if (csharp && compiled == null) start.ArgumentList.Add("run");
  start.ArgumentList.Add(compiled ?? guard);
  if (compiled == null) start.ArgumentList.Add("--");
  foreach (string arg in guardArgs) start.ArgumentList.Add(arg);
  string root = GitRoot();
  start.Environment["CLAUDE_PROJECT_DIR"] = root;
  start.Environment["CODEX_PROJECT_DIR"] = root;
  if (!string.IsNullOrWhiteSpace(runtime)) start.Environment["AGENT_HOOK_RUNTIME"] = runtime;
  start.Environment["AGENT_HOOK_BUILD_DEADLINE"] = HookCache.Deadline.ToString("O");
  using Process process = Process.Start(start)!;
  // A verbose guard can fill stderr while stdout is still open (and can write before reading
  // stdin). Drain both pipes immediately so neither side waits for the runtime's hook timeout.
  var stdout = process.StandardOutput.ReadToEndAsync();
  var stderr = process.StandardError.ReadToEndAsync();
  process.StandardInput.Write(input);
  process.StandardInput.Close();
  process.WaitForExit();
  return (process.ExitCode, stdout.GetAwaiter().GetResult() + stderr.GetAwaiter().GetResult());
}

static string GitRoot() {
  var start = new ProcessStartInfo("git") { RedirectStandardOutput = true };
  start.ArgumentList.Add("rev-parse");
  start.ArgumentList.Add("--show-toplevel");
  using Process process = Process.Start(start)!;
  string root = process.StandardOutput.ReadToEnd().Trim();
  process.WaitForExit();
  return root.Length > 0 ? root : Directory.GetCurrentDirectory();
}

// This is an execution cache for the existing scripts, not a second implementation of any rule.
// Its fast path hashes inputs and the complete private output bundle without starting the SDK.
// Unsupported MSBuild customization retains the original SDK path instead of guessing inputs.
static class HookCache {
  public static bool Supports(string script) => Inputs(Path.GetFullPath(script)) != null;
  public static readonly DateTime Deadline = BuildDeadline();
  static DateTime BuildDeadline() {
    DateTime local = DateTime.UtcNow.AddSeconds(45);
    if (Environment.GetCommandLineArgs().Contains("--prepare-hooks")) return DateTime.MaxValue;
    return DateTime.TryParse(Environment.GetEnvironmentVariable("AGENT_HOOK_BUILD_DEADLINE"),
      System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind, out var inherited) && inherited < local
      ? inherited : local;
  }
  static int BuildMilliseconds => (int)Math.Clamp((Deadline - DateTime.UtcNow).TotalMilliseconds, 0, 120_000);
  public static string SourcePath([System.Runtime.CompilerServices.CallerFilePath] string path = "") => path;
  static string CacheRoot => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(SourcePath())!, "..", "out", "agent-hooks"));

  public static string? Resolve(string script, bool build = true) {
    script = Path.GetFullPath(script);
    string? fingerprint = Fingerprint(script);
    if (fingerprint == null) return null;
    string parent = Path.Combine(CacheRoot, Hash(Encoding.UTF8.GetBytes(script)));
    string destination = Path.Combine(parent, fingerprint);
    string? ready = ReadReady(destination, fingerprint);
    if (ready != null || !build) return ready;
    Directory.CreateDirectory(parent);
    // Serialize cold SDK builds only. Warm checks never acquire this lease, and concurrent
    // installers/hooks cannot publish partial output or race on the shared project compiler.
    using var lease = Acquire(Path.Combine(CacheRoot, "build.lock"), () => ReadReady(destination, fingerprint) != null);
    ready = ReadReady(destination, fingerprint);
    if (ready != null) return ready;
    string temporary = Path.Combine(parent, ".building-" + Guid.NewGuid().ToString("N"));
    string sources = temporary + ".sources";
    Directory.CreateDirectory(temporary);
    try {
      // Build a private snapshot: --output alone still lets referenced projects overwrite
      // the development checkout's shared obj/ref files during another agent's build.
      string filesystemRoot = Path.GetPathRoot(script)!;
      string SnapshotPath(string file) {
        string relative = Path.GetRelativePath(filesystemRoot, file);
        if (Path.IsPathRooted(relative) || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal))
          throw new InvalidOperationException("Hook dependencies must share a filesystem root");
        return Path.Combine(sources, relative);
      }
      foreach (string input in Inputs(script) ?? throw new InvalidOperationException("Hook build inputs changed")) {
        if (!File.Exists(input)) continue;
        string target = SnapshotPath(input);
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        File.Copy(input, target);
      }
      if (Fingerprint(script, SnapshotPath) != fingerprint)
        throw new InvalidOperationException("Hook inputs changed while taking the build snapshot; retry with stable source");
      // A script originally outside the repository must not accidentally inherit the
      // enclosing cache directory's repository props when compiled in this snapshot.
      foreach (string boundary in new[] { "Directory.Build.props", "Directory.Build.targets" }) {
        string boundaryPath = Path.Combine(sources, boundary);
        if (!File.Exists(boundaryPath)) File.WriteAllText(boundaryPath, "<Project />");
      }
      var start = new ProcessStartInfo("dotnet") { RedirectStandardOutput = true, RedirectStandardError = true };
      foreach (var argument in new[] { "build", SnapshotPath(script), "--output", temporary, "--no-incremental",
          "-p:PathMap=" + sources + "=" + filesystemRoot,
          "--disable-build-servers", "--nologo", "--verbosity", "quiet" }) start.ArgumentList.Add(argument);
      using var process = Process.Start(start)!;
      var stdout = process.StandardOutput.ReadToEndAsync();
      var stderr = process.StandardError.ReadToEndAsync();
      if (!process.WaitForExit(BuildMilliseconds)) {
        process.Kill(entireProcessTree: true);
        process.WaitForExit();
        throw new InvalidOperationException($"Compilation timed out for {Path.GetFileName(script)}; no stale guard was used");
      }
      if (process.ExitCode != 0) throw new InvalidOperationException($"Cannot compile {Path.GetFileName(script)}: {stdout.Result}{stderr.Result}");
      if (Fingerprint(script) != fingerprint) throw new InvalidOperationException("Hook inputs changed during compilation; retry with stable source");
      string assembly = Path.GetFileNameWithoutExtension(script) + ".dll";
      if (!File.Exists(Path.Combine(temporary, assembly))) throw new InvalidOperationException("Hook compilation produced no entry assembly");
      var files = Directory.GetFiles(temporary, "*", SearchOption.AllDirectories)
        .ToDictionary(file => Path.GetRelativePath(temporary, file), FileHash, StringComparer.Ordinal);
      File.WriteAllText(Path.Combine(temporary, "qualification.json"), ZJson.SerializeObject(new Qualification {
        Fingerprint = fingerprint, Assembly = assembly, Files = files,
      }));
      // A corrupt entry is retained for diagnosis; never repair a bundle in place while a
      // running process could have mapped some of its assemblies.
      if (Directory.Exists(destination)) Directory.Move(destination, destination + ".invalid-" + Guid.NewGuid().ToString("N"));
      Directory.Move(temporary, destination);
      return ReadReady(destination, fingerprint) ?? throw new InvalidOperationException("Compiled hook failed output verification");
    } finally {
      if (Directory.Exists(temporary)) Directory.Delete(temporary, recursive: true);
      if (Directory.Exists(sources)) Directory.Delete(sources, recursive: true);
    }
  }

  static string? ReadReady(string directory, string fingerprint) {
    try {
      var manifest = ZJson.DeserializeObject<Qualification>(null, File.ReadAllText(Path.Combine(directory, "qualification.json")));
      if (manifest == null || manifest.Fingerprint != fingerprint || manifest.Files.Count == 0 ||
          Path.GetFileName(manifest.Assembly) != manifest.Assembly || !manifest.Files.ContainsKey(manifest.Assembly)) return null;
      foreach (var file in manifest.Files) {
        string full = Path.GetFullPath(Path.Combine(directory, file.Key));
        if (!full.StartsWith(directory + Path.DirectorySeparatorChar, StringComparison.Ordinal) || FileHash(full) != file.Value) return null;
      }
      return Path.Combine(directory, manifest.Assembly);
    } catch (Exception) { return null; } // A malformed qualification is also a cache miss, never permission to run it.
  }

  static FileStream? Acquire(string path, Func<bool> ready) {
    var timer = Stopwatch.StartNew();
    while (true) {
      try { return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
      catch (IOException) {
        if (ready()) return null;
        if (BuildMilliseconds == 0 || timer.Elapsed > TimeSpan.FromSeconds(120))
          throw new InvalidOperationException("Hook build cache is busy; run ci/install-hooks.sh to prepare it before retrying");
        Thread.Sleep(100);
      }
    }
  }

  static SortedSet<string>? Inputs(string script) {
    var inputs = new SortedSet<string>(StringComparer.Ordinal) { script };
    string source = File.ReadAllText(script);
    var directives = source.Split('\n').Where(line => line.TrimStart().StartsWith("#:", StringComparison.Ordinal)).ToArray();
    if (directives.Any(line => !line.TrimStart().StartsWith("#:project ", StringComparison.Ordinal))) return null;
    foreach (var directive in directives) {
      string reference = directive.Trim()[10..].Trim().Trim('"');
      string project = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(script)!, reference));
      // All current guards share this single, ordinary SDK project. Don't cache a newly added
      // project/import/analyzer/linked-source arrangement without an actual input inventory.
      if (Path.GetFileName(project) != "ZCore.csproj" || !OrdinaryProject(project)) return null;
      inputs.Add(project);
      string projectDirectory = Path.GetDirectoryName(project)!;
      foreach (string file in SourceFiles(projectDirectory)) inputs.Add(file);
    }
    var visited = new HashSet<string>(StringComparer.Ordinal);
    foreach (string start in inputs.Select(Path.GetDirectoryName).OfType<string>().Distinct().ToArray()) {
      for (string? directory = start; directory != null; directory = Path.GetDirectoryName(directory)) {
        if (!visited.Add(directory)) break;
        foreach (string name in new[] { "Directory.Build.props", "Directory.Build.targets", "Directory.Packages.props",
            "global.json", "NuGet.Config", "nuget.config", ".editorconfig" }) {
          string path = Path.Combine(directory, name);
          if (File.Exists(path) && (name.EndsWith(".props", StringComparison.Ordinal) || name.EndsWith(".targets", StringComparison.Ordinal)) && !OrdinaryProject(path)) return null;
          inputs.Add(path); // Missing files are inputs too: adding a build override invalidates the bundle.
        }
      }
    }
    return inputs;
  }

  static string? Fingerprint(string script, Func<string, string>? snapshotPath = null) {
    var inputs = Inputs(script);
    if (inputs == null) return null;
    using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
    hash.AppendData(Encoding.UTF8.GetBytes("agent-hooks-v2\n" + System.Runtime.InteropServices.RuntimeInformation.RuntimeIdentifier + "\n" + Environment.Version + "\n"));
    foreach (string path in inputs) {
      hash.AppendData(Encoding.UTF8.GetBytes(path + "\0"));
      hash.AppendData(Encoding.UTF8.GetBytes(File.Exists(path) ? FileHash(snapshotPath?.Invoke(path) ?? path) : "missing"));
    }
    return Convert.ToHexString(hash.GetHashAndReset());
  }

  static IEnumerable<string> SourceFiles(string directory) {
    foreach (var file in Directory.EnumerateFiles(directory)) yield return file;
    foreach (var child in Directory.EnumerateDirectories(directory)) {
      if (Path.GetFileName(child) is "bin" or "obj" or ".git") continue;
      if ((File.GetAttributes(child) & FileAttributes.ReparsePoint) != 0)
        throw new InvalidOperationException("Hook dependency directory symlink requires an explicit build-input inventory");
      foreach (var file in SourceFiles(child)) yield return file;
    }
  }

  static bool OrdinaryProject(string path) => !XDocument.Load(path).Descendants().Any(element =>
    element.Name.LocalName is "Import" or "ProjectReference" or "Compile" or "Analyzer" or "AdditionalFiles" or "Reference" or "Target" ||
    element.Attributes().Any(attribute => attribute.Name.LocalName is "Condition" && attribute.Value.Contains("Exists(", StringComparison.OrdinalIgnoreCase)));
  static string FileHash(string path) { using var stream = File.OpenRead(path); return Convert.ToHexString(SHA256.HashData(stream)); }
  static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
  sealed class Qualification {
    public string Fingerprint { get; set; } = "";
    public string Assembly { get; set; } = "";
    public Dictionary<string, string> Files { get; set; } = new(StringComparer.Ordinal);
  }
}

class HookEnvelope {
  [JsonPropertyName("tool_name")] public string? ToolName { get; set; }
  [JsonPropertyName("tool_input")] public HookToolInput? ToolInput { get; set; }
}

class HookToolInput {
  [JsonPropertyName("command")] public string? Command { get; set; }
  [JsonPropertyName("file_path")] public string? FilePath { get; set; }
  [JsonPropertyName("old_string")] public string? OldString { get; set; }
  [JsonPropertyName("new_string")] public string? NewString { get; set; }
}

record PatchFile(string Path, string Added, string Removed);

/// <summary>The full Claude payload a translated Grok call becomes (Write carries `content`, Edit `replace_all`).</summary>
class GuardEnvelope {
  [JsonPropertyName("tool_name")] public string ToolName { get; set; } = "";
  [JsonPropertyName("tool_input")] public GuardToolInput ToolInput { get; set; } = new();
}

class GuardToolInput {
  [JsonPropertyName("command")] public string? Command { get; set; }
  [JsonPropertyName("file_path")] public string? FilePath { get; set; }
  [JsonPropertyName("content")] public string? Content { get; set; }
  [JsonPropertyName("old_string")] public string? OldString { get; set; }
  [JsonPropertyName("new_string")] public string? NewString { get; set; }
  [JsonPropertyName("replace_all")] public bool ReplaceAll { get; set; }
  [JsonPropertyName("edits")] public object? Edits { get; set; }
}

class GrokEnvelope {
  [JsonPropertyName("toolName")] public string? ToolName { get; set; }
  [JsonPropertyName("toolInput")] public GrokToolInput? ToolInput { get; set; }
}

class GrokToolInput {
  [JsonPropertyName("command")] public string? Command { get; set; }
  [JsonPropertyName("path")] public string? Path { get; set; }
  [JsonPropertyName("file_path")] public string? FilePath { get; set; }
  [JsonPropertyName("filePath")] public string? FilePathCamel { get; set; }
  [JsonPropertyName("content")] public string? Content { get; set; }
  [JsonPropertyName("old_string")] public string? OldString { get; set; }
  [JsonPropertyName("oldString")] public string? OldStringCamel { get; set; }
  [JsonPropertyName("new_string")] public string? NewString { get; set; }
  [JsonPropertyName("newString")] public string? NewStringCamel { get; set; }
  [JsonPropertyName("replace_all")] public bool? ReplaceAll { get; set; }
  [JsonPropertyName("replaceAll")] public bool? ReplaceAllCamel { get; set; }
  [JsonPropertyName("edits")] public object? Edits { get; set; }
}

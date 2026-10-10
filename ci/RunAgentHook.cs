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
if (args.FirstOrDefault() == "--self-test-cache-identity") {
  HookCache.TestCacheIdentity();
  return 0;
}
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
  if (hook?.ToolName == "exec_command") {
    hook.ToolName = "Bash";
    if (hook.ToolInput != null) hook.ToolInput.Command ??= hook.ToolInput.Cmd;
    yield return ZJson.SerializeObject(hook);
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
  if (csharp && compiled == null) {
    start.ArgumentList.Add("run");
    start.ArgumentList.Add("--disable-build-servers");
  }
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
  static string? fixtureSource;
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
  public static string SourcePath([System.Runtime.CompilerServices.CallerFilePath] string path = "") {
    if (fixtureSource != null) return fixtureSource;
    // A qualified bundle can move between agent checkouts. Its baked CallerFilePath
    // still names the build host; resolve the source beside the current private cache.
    for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory != null; directory = directory.Parent) {
      string current = Path.Combine(directory.FullName, "ci", "RunAgentHook.cs");
      if (File.Exists(current)) return current;
    }
    return path; // SDK-launched file apps live outside the engine's private cache.
  }
  static string RepositoryRoot() {
    string engine = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(SourcePath())!, ".."));
    string consumer = Path.GetDirectoryName(engine)!;
    return File.Exists(Path.Combine(consumer, ".git")) || Directory.Exists(Path.Combine(consumer, ".git")) ? consumer : engine;
  }
  static string? InputIdentity(string path) {
    string relative = Path.GetRelativePath(RepositoryRoot(), Path.GetFullPath(path)).Replace('\\', '/');
    return Path.IsPathRooted(relative) || relative == ".." || relative.StartsWith("../", StringComparison.Ordinal) ? null : relative;
  }
  static string CacheRoot => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(SourcePath())!, "..", "out", "agent-hooks"));

  public static string? Resolve(string script, bool build = true) {
    script = Path.GetFullPath(script);
    string? fingerprint = Fingerprint(script);
    if (fingerprint == null) return null;
    string parent = Path.Combine(CacheRoot, Hash(Encoding.UTF8.GetBytes(InputIdentity(script)!)));
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
    if (InputIdentity(script) == null) return null;
    var inputs = new SortedSet<string>(StringComparer.Ordinal) { script };
    string source = File.ReadAllText(script);
    var directives = source.Split('\n').Where(line => line.TrimStart().StartsWith("#:", StringComparison.Ordinal)).ToArray();
    if (directives.Any(line => !line.TrimStart().StartsWith("#:project ", StringComparison.Ordinal))) return null;
    foreach (var directive in directives) {
      string reference = directive.Trim()[10..].Trim().Trim('"');
      string project = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(script)!, reference));
      if (InputIdentity(project) == null) return null;
      // All current guards share this single, ordinary SDK project. Don't cache a newly added
      // project/import/analyzer/linked-source arrangement without an actual input inventory.
      if (Path.GetFileName(project) != "ZCore.csproj" || !OrdinaryProject(project)) return null;
      inputs.Add(project);
      string projectDirectory = Path.GetDirectoryName(project)!;
      foreach (string file in SourceFiles(projectDirectory)) inputs.Add(file);
    }
    var visited = new HashSet<string>(StringComparer.Ordinal);
    string repository = RepositoryRoot();
    if (inputs.Any(input => InputIdentity(input) == null)) return null;
    foreach (string start in inputs.Select(Path.GetDirectoryName).OfType<string>().Distinct().ToArray()) {
      for (string? directory = start; directory != null; directory = Path.GetDirectoryName(directory)) {
        if (!visited.Add(directory)) break;
        foreach (string name in new[] { "Directory.Build.props", "Directory.Build.targets", "Directory.Packages.props",
            "global.json", "NuGet.Config", "nuget.config", ".editorconfig" }) {
          string path = Path.Combine(directory, name);
          if (File.Exists(path) && (name.EndsWith(".props", StringComparison.Ordinal) || name.EndsWith(".targets", StringComparison.Ordinal)) && !OrdinaryProject(path)) return null;
          inputs.Add(path); // Missing files are inputs too: adding a build override invalidates the bundle.
        }
        if (directory == repository) break;
      }
    }
    return inputs;
  }

  static string? Fingerprint(string script, Func<string, string>? snapshotPath = null) {
    var inputs = Inputs(script);
    if (inputs == null) return null;
    using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
    hash.AppendData(Encoding.UTF8.GetBytes("agent-hooks-v3\n" + System.Runtime.InteropServices.RuntimeInformation.RuntimeIdentifier + "\n" + Environment.Version + "\n"));
    foreach (string path in inputs.OrderBy(path => InputIdentity(path), StringComparer.Ordinal)) {
      hash.AppendData(Encoding.UTF8.GetBytes(InputIdentity(path) + "\0"));
      hash.AppendData(Encoding.UTF8.GetBytes(File.Exists(path) ? FileHash(snapshotPath?.Invoke(path) ?? path) : "missing"));
    }
    return Convert.ToHexString(hash.GetHashAndReset());
  }

  static IEnumerable<string> SourceFiles(string directory) {
    foreach (var file in Directory.EnumerateFiles(directory)) {
      // Unity import metadata does not participate in the ordinary SDK compilation.
      if (!Path.GetExtension(file).Equals(".meta", StringComparison.OrdinalIgnoreCase)) yield return file;
    }
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

  public static void TestCacheIdentity() {
    string fixtures = Path.Combine(RepositoryRoot(), ".scratch", "cache-identity-fixtures", Guid.NewGuid().ToString("N"));
    string? previousSource = fixtureSource;
    Directory.CreateDirectory(fixtures);
    try {
      string? expected = null;
      string? expectedFingerprint = null;
      foreach (string name in new[] { "candidate", "primary" }) {
        string repository = Path.Combine(fixtures, name);
        string engineCi = Path.Combine(repository, "inzania-engine", "ci");
        Directory.CreateDirectory(engineCi);
        var initialize = new ProcessStartInfo("git") { RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (string argument in new[] { "init", "-q", repository }) initialize.ArgumentList.Add(argument);
        foreach (string key in initialize.Environment.Keys.Where(key => key.StartsWith("GIT_", StringComparison.Ordinal)).ToArray()) initialize.Environment.Remove(key);
        using (var process = Process.Start(initialize)!) {
          var stdout = process.StandardOutput.ReadToEndAsync();
          var stderr = process.StandardError.ReadToEndAsync();
          if (!process.WaitForExit(10000)) { process.Kill(true); process.WaitForExit(); throw new InvalidOperationException("fixture Git initialization timed out"); }
          Task.WhenAll(stdout, stderr).GetAwaiter().GetResult();
          if (process.ExitCode != 0) throw new InvalidOperationException("fixture Git initialization failed");
        }
        fixtureSource = Path.Combine(engineCi, "RunAgentHook.cs");
        File.WriteAllText(fixtureSource, "// fixture adapter source owner\n");
        string script = Path.Combine(engineCi, "Fixture.cs");
        string dependencyDirectory = Path.Combine(repository, "inzania-engine", "ZCore");
        Directory.CreateDirectory(dependencyDirectory);
        File.WriteAllText(Path.Combine(dependencyDirectory, "ZCore.csproj"),
          "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>");
        string dependency = Path.Combine(dependencyDirectory, "Dependency.cs");
        const string dependencyContents = "public static class Dependency { public const string Value = \"fixture\"; }\n";
        File.WriteAllText(dependency, dependencyContents);
        if (name == "primary") {
          File.WriteAllText(dependency + ".meta", "fileFormatVersion: 2\nguid: fixture\n");
          File.WriteAllText(Path.Combine(dependencyDirectory, "obj.meta"), "fileFormatVersion: 2\nguid: folder\n");
        }
        const string contents = "#:project ../ZCore/ZCore.csproj\nusing System; Console.WriteLine(Dependency.Value);\n";
        const string overrides = "<Project><PropertyGroup><Nullable>enable</Nullable></PropertyGroup></Project>";
        File.WriteAllText(script, contents);
        string props = Path.Combine(repository, "Directory.Build.props");
        File.WriteAllText(props, overrides);
        string fingerprint = Fingerprint(script) ?? throw new InvalidOperationException("fixture inputs unsupported");
        string executable = Resolve(script, build: true) ?? throw new InvalidOperationException("fixture did not compile");
        string relative = Path.GetRelativePath(repository, executable).Replace('\\', '/');
        if (expected != null && (relative != expected || fingerprint != expectedFingerprint))
          throw new InvalidOperationException("identical source in different worktrees produces different manifest executable paths");
        expected = relative;
        expectedFingerprint = fingerprint;
        if (Resolve(script, build: false) != executable) throw new InvalidOperationException("warm fixture cache did not qualify");
        File.AppendAllText(dependency, "// changed dependency content\n");
        if (Fingerprint(script) == fingerprint || Resolve(script, build: false) != null)
          throw new InvalidOperationException("changed C# dependency retained a qualified cache entry");
        File.WriteAllText(dependency, dependencyContents);
        string resource = Path.Combine(dependencyDirectory, "Added.resx");
        File.WriteAllText(resource, "<root />");
        if (Fingerprint(script) == fingerprint || Resolve(script, build: false) != null)
          throw new InvalidOperationException("new resource retained a qualified cache entry");
        File.Delete(resource);
        File.AppendAllText(script, "// changed content\n");
        if (Fingerprint(script) == fingerprint || Resolve(script, build: false) != null)
          throw new InvalidOperationException("changed source retained a qualified cache entry");
        File.WriteAllText(script, contents);
        File.WriteAllText(props, "<Project><PropertyGroup><Nullable>disable</Nullable></PropertyGroup></Project>");
        if (Fingerprint(script) == fingerprint) throw new InvalidOperationException("build override was omitted");
        File.WriteAllText(props, overrides);
        string targets = Path.Combine(repository, "Directory.Build.targets");
        File.WriteAllText(targets, "<Project><PropertyGroup><Fixture>changed</Fixture></PropertyGroup></Project>");
        if (Fingerprint(script) == fingerprint) throw new InvalidOperationException("new build override was omitted");
        File.Delete(targets);
        string outside = Path.Combine(fixtures, "Outside.cs");
        File.WriteAllText(outside, contents);
        if (Fingerprint(outside) != null || Resolve(outside, build: false) != null)
          throw new InvalidOperationException("outside-root script was admitted");
      }
      Console.WriteLine("CACHE_IDENTITY_PATH " + expected);
      Console.WriteLine("PASS compiled cross-worktree cache identity with asymmetric Unity metadata, warm qualification, source/dependency/build override invalidation and outside-root refusal");
    } finally {
      fixtureSource = previousSource;
      Directory.Delete(fixtures, recursive: true);
    }
    TestRelocatedAdapter();
  }

  static void TestRelocatedAdapter() {
    string adapter = SourcePath();
    string repository = RepositoryRoot();
    var inputs = Inputs(adapter) ?? throw new InvalidOperationException("adapter relocation inputs unsupported");
    string fixtures = Path.Combine(repository, ".scratch", "adapter-relocation-fixtures", Guid.NewGuid().ToString("N"));
    string original = Path.Combine(fixtures, "original");
    string relocated = Path.Combine(fixtures, "relocated");
    string? previousSource = fixtureSource;
    try {
      foreach (string input in inputs.Where(File.Exists)) {
        string target = Path.Combine(original, Path.GetRelativePath(repository, input));
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        File.Copy(input, target);
      }
      Directory.CreateDirectory(Path.Combine(original, ".git"));
      fixtureSource = Path.Combine(original, Path.GetRelativePath(repository, adapter));
      string executable = Resolve(fixtureSource) ?? throw new InvalidOperationException("adapter fixture did not compile");
      string relativeExecutable = Path.GetRelativePath(original, executable);
      string guard = "RelocationGuard.sh";
      File.WriteAllText(Path.Combine(original, guard), "cat >/dev/null\nprintf 'RELOCATED_GUARD_DENIES\\n' >&2\nexit 2\n");
      foreach (string file in Directory.GetFiles(original, "*", SearchOption.AllDirectories)) {
        string target = Path.Combine(relocated, Path.GetRelativePath(original, file));
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        File.Copy(file, target);
      }
      Directory.CreateDirectory(Path.Combine(relocated, ".git"));
      Directory.Delete(original, recursive: true);
      fixtureSource = previousSource;
      var start = new ProcessStartInfo("dotnet") {
        WorkingDirectory = relocated, RedirectStandardInput = true,
        RedirectStandardOutput = true, RedirectStandardError = true,
      };
      foreach (string argument in new[] { Path.Combine(relocated, relativeExecutable), "--guard", Path.Combine(relocated, guard) })
        start.ArgumentList.Add(argument);
      start.Environment["MSBuildSDKsPath"] = Path.Combine(fixtures, "absent-sdk");
      using var process = Process.Start(start)!;
      var stdout = process.StandardOutput.ReadToEndAsync();
      var stderr = process.StandardError.ReadToEndAsync();
      process.StandardInput.Close();
      if (!process.WaitForExit(60000)) {
        process.Kill(true);
        process.WaitForExit();
        throw new InvalidOperationException("relocated adapter fixture timed out");
      }
      string output = stdout.GetAwaiter().GetResult() + stderr.GetAwaiter().GetResult();
      if (process.ExitCode != 2 || !output.Contains("RELOCATED_GUARD_DENIES", StringComparison.Ordinal) ||
          output.Contains("AGENT_HOOK_UNAVAILABLE", StringComparison.Ordinal))
        throw new InvalidOperationException("relocated adapter could not run without its original checkout or SDK: " + output);
      Console.WriteLine("PASS relocated compiled adapter executes the real guard without original checkout or SDK");
    } finally {
      fixtureSource = previousSource;
      if (Directory.Exists(fixtures)) Directory.Delete(fixtures, recursive: true);
    }
  }
  sealed class Qualification {
    public string Fingerprint { get; set; } = "";
    public string Assembly { get; set; } = "";
    public Dictionary<string, string> Files { get; set; } = new(StringComparer.Ordinal);
  }
}

class HookEnvelope {
  [JsonPropertyName("tool_name")] public string? ToolName { get; set; }
  [JsonPropertyName("tool_input")] public HookToolInput? ToolInput { get; set; }
  [JsonExtensionData] public Dictionary<string, object>? Metadata { get; set; }
}

class HookToolInput {
  [JsonPropertyName("cmd")] public string? Cmd { get; set; }
  [JsonPropertyName("command")] public string? Command { get; set; }
  [JsonPropertyName("file_path")] public string? FilePath { get; set; }
  [JsonPropertyName("old_string")] public string? OldString { get; set; }
  [JsonPropertyName("new_string")] public string? NewString { get; set; }
  [JsonExtensionData] public Dictionary<string, object>? Metadata { get; set; }
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

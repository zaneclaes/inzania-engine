#:project ../../ZCore/ZCore.csproj
// Generic structural validator for a repository-managed Datadog dashboard. Adapted from the
// applicable checks in hickoryai/hickory-ai .agents/hooks/rules/datadog_dashboard_valid.py, blob
// f806299164a5bd69b35a26516d1da527845a9631 (2026-09-14); attribution retained, but this is a
// C#/ZJson implementation rather than Hickory's Python hook/permissions. A consuming repository
// supplies its dashboard with the hook argument `--path <repo-relative path>`.
//
// The monitor-only `.as_count()` time-aggregation restriction from Hickory's
// no_as_count_nonsum_aggregator.py is intentionally not applied here: dashboard metric queries
// have different grammar and valid dashboard count visualizations use that modifier.
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using IZ.Core.Contexts;
using IZ.Core.Json;
using IZ.Core.Tooling;

ZScriptApp.Start("DataDogDashboardGuard");

string[] cli = Environment.GetCommandLineArgs().Skip(1).ToArray();
if (cli.Contains("--check")) {
  string? path = ValueAfter(cli, "--check");
  if (string.IsNullOrWhiteSpace(path)) return ConfigurationError("--check requires a dashboard path");
  return Print(Validate(File.ReadAllText(path), path), path, 1);
}

string? configuredPath = ValueAfter(cli, "--path");
if (string.IsNullOrWhiteSpace(configuredPath)) return ConfigurationError("--path <repo-relative dashboard path> is required");

var hook = ClaudeHookInput.Read(Console.In.ReadToEnd());
var input = hook?.ToolInput;
string file = input?.FilePath ?? "";
if (input == null || !input.WritesText || !IsConfiguredPath(file, configuredPath)) return 0;
string existing = File.Exists(file) ? File.ReadAllText(file) : "";
var errors = Validate(input.ProspectiveContent(existing), file);
return Print(errors, file, 2);

static int ConfigurationError(string error) {
  Console.Error.WriteLine($"datadog-dashboard-guard configuration error: {error}");
  return 2;
}

static string? ValueAfter(IEnumerable<string> args, string option) {
  string[] values = args.ToArray();
  int index = Array.IndexOf(values, option);
  return index >= 0 && index < values.Length - 1 ? values[index + 1] : null;
}

static bool IsConfiguredPath(string file, string configuredPath) {
  if (string.IsNullOrWhiteSpace(file)) return false;
  string root = Environment.GetEnvironmentVariable("CLAUDE_PROJECT_DIR") ??
                Environment.GetEnvironmentVariable("CODEX_PROJECT_DIR") ?? Directory.GetCurrentDirectory();
  return string.Equals(Path.GetFullPath(file), Path.GetFullPath(configuredPath, root), StringComparison.Ordinal);
}

static int Print(IReadOnlyCollection<string> errors, string path, int failureCode) {
  if (errors.Count <= 0) return 0;
  Console.Error.WriteLine($"datadog-dashboard-guard BLOCKED {path}:");
  foreach (string error in errors.Take(20)) Console.Error.WriteLine("  - " + error);
  if (errors.Count > 20) Console.Error.WriteLine($"  - … and {errors.Count - 20} more");
  return failureCode;
}

static List<string> Validate(string json, string path) {
  Dashboard? dashboard;
  Dictionary<string, object?>? document;
  try {
    dashboard = ZJson.DeserializeObject<Dashboard>(null, json);
    document = ZJson.DeserializeObject<Dictionary<string, object?>>(null, json, new ZJsonSerializationOpts { ObjectsAsDictionaries = true });
  } catch (Exception e) {
    return new List<string> { $"is not valid JSON: {e.Message}" };
  }

  var errors = new List<string>();
  if (dashboard == null) return new List<string> { "does not contain a dashboard object" };
  if (string.IsNullOrWhiteSpace(dashboard.Title)) errors.Add("title is required");
  if (!IsLayout(dashboard.LayoutType)) errors.Add("root layout_type must be ordered or free");
  if (dashboard.Widgets == null || dashboard.Widgets.Count <= 0) errors.Add("at least one top-level widget is required");

  var ids = new HashSet<long>();
  ValidateWidgets(dashboard.Widgets ?? new List<Widget>(), "root", ids, errors);
  ValidateWidgetDefinitionSchemas(Objects(document, "widgets"), "root", errors);
  ValidateTabs(dashboard, ids, errors);
  return errors;
}

static void ValidateWidgetDefinitionSchemas(IReadOnlyCollection<Dictionary<string, object?>>? widgets, string parent, List<string> errors) {
  if (widgets == null) return;
  foreach (Dictionary<string, object?> widget in widgets) {
    long id = widget.TryGetValue("id", out object? value) && value is long number ? number : 0;
    string label = id > 0 ? $"widget {id}" : "widget";
    if (!widget.TryGetValue("definition", out object? rawDefinition) || rawDefinition is not Dictionary<string, object?> definition) continue;
    string? type = definition.GetValueOrDefault("type") as string;
    if (string.IsNullOrWhiteSpace(type) || !IsType(type)) continue;

    if (type == "note") {
      var allowed = new HashSet<string>(StringComparer.Ordinal) { "background_color", "content", "font_size", "has_padding", "show_tick", "text_align", "tick_edge", "tick_pos", "type", "vertical_align" };
      foreach (string property in definition.Keys.Where(property => !allowed.Contains(property)))
        errors.Add($"{parent}/{label}: note does not allow property '{property}'");
      if (!definition.TryGetValue("content", out object? content) || content is not string text || string.IsNullOrWhiteSpace(text))
        errors.Add($"{parent}/{label}: note requires content");
    } else if (type == "group")
      ValidateWidgetDefinitionSchemas(Objects(definition, "widgets"), parent + "/" + label, errors);
  }
}

static IReadOnlyCollection<Dictionary<string, object?>>? Objects(Dictionary<string, object?>? parent, string property) {
  if (parent == null || !parent.TryGetValue(property, out object? value) || value is not List<object?> values) return null;
  return values.OfType<Dictionary<string, object?>>().ToList();
}

static void ValidateWidgets(IReadOnlyCollection<Widget> widgets, string parent, HashSet<long> ids, List<string> errors) {
  var rectangles = new List<(int X, int Y, int Width, int Height, long Id)>();
  foreach (Widget widget in widgets) {
    long id = widget.Id ?? 0;
    if (id <= 0 || id > 9007199254740991L) {
      errors.Add($"{parent}: widget id must be a positive JavaScript-safe integer");
    } else if (!ids.Add(id)) {
      errors.Add($"{parent}: widget id {id} is duplicated");
    }

    string label = id > 0 ? $"widget {id}" : "widget";
    if (widget.Definition == null) {
      errors.Add($"{parent}/{label}: definition is required");
      continue;
    }
    string? type = widget.Definition.Type;
    if (string.IsNullOrWhiteSpace(type) || !IsType(type)) errors.Add($"{parent}/{label}: unsupported widget type '{type ?? "(missing)"}'");
    if (type != "note" && string.IsNullOrWhiteSpace(widget.Definition.Title)) errors.Add($"{parent}/{label}: title is required");
    if (widget.Layout == null || widget.Layout.Width <= 0 || widget.Layout.Height <= 0 || widget.Layout.X < 0 || widget.Layout.Y < 0 || widget.Layout.X + widget.Layout.Width > 12) {
      errors.Add($"{parent}/{label}: layout must be a positive rectangle inside the 12-column grid");
    } else {
      rectangles.Add((widget.Layout.X, widget.Layout.Y, widget.Layout.Width, widget.Layout.Height, id));
    }

    if (type == "group") {
      if (!IsLayout(widget.Definition.LayoutType)) errors.Add($"{parent}/{label}: group layout_type must be ordered or free");
      if (widget.Definition.Widgets == null || widget.Definition.Widgets.Count <= 0) errors.Add($"{parent}/{label}: group must contain widgets");
      else ValidateWidgets(widget.Definition.Widgets, parent + "/" + label, ids, errors);
    } else if (type != "note") {
      ValidateRequests(widget.Definition.Requests, parent + "/" + label, errors);
    }
  }

  for (int i = 0; i < rectangles.Count; i++) {
    for (int j = i + 1; j < rectangles.Count; j++) {
      var a = rectangles[i];
      var b = rectangles[j];
      if (a.X < b.X + b.Width && b.X < a.X + a.Width && a.Y < b.Y + b.Height && b.Y < a.Y + a.Height)
        errors.Add($"{parent}: widgets {a.Id} and {b.Id} overlap");
    }
  }
}

static void ValidateRequests(List<Request>? requests, string path, List<string> errors) {
  if (requests == null || requests.Count <= 0) {
    errors.Add($"{path}: non-note widget needs a request");
    return;
  }
  foreach (Request request in requests) {
    var names = new HashSet<string>();
    foreach (Query query in request.Queries ?? new List<Query>()) {
      if (string.IsNullOrWhiteSpace(query.Name) || !names.Add(query.Name)) errors.Add($"{path}: query names must be present and unique");
      if (string.IsNullOrWhiteSpace(query.DataSource)) errors.Add($"{path}: query data_source is required");
      if (string.IsNullOrWhiteSpace(query.Text)) errors.Add($"{path}: query text is required");
      else ValidateQuery(query.Text, path, errors);
    }
    foreach (Formula formula in request.Formulas ?? new List<Formula>()) {
      if (string.IsNullOrWhiteSpace(formula.Text)) errors.Add($"{path}: formula text is required");
      else foreach (Match match in Regex.Matches(formula.Text, @"\b[A-Za-z_]\w*\b")) {
        if (!names.Contains(match.Value) && match.Value is not "true" and not "false")
          errors.Add($"{path}: formula references unknown query '{match.Value}'");
      }
    }
  }
}

static void ValidateQuery(string query, string path, List<string> errors) {
  foreach (Match filter in Regex.Matches(query, @"\{([^{}]*)\}")) {
    string text = filter.Groups[1].Value;
    if (text.Contains(',') && Regex.IsMatch(text, @"\b(and|or)\b", RegexOptions.IgnoreCase))
      errors.Add($"{path}: query mixes comma and boolean filter syntax: {query}");
  }
  if (query.Contains("by {email}", StringComparison.OrdinalIgnoreCase) ||
      query.Contains("by {user_id}", StringComparison.OrdinalIgnoreCase) ||
      query.Contains("by {score_id}", StringComparison.OrdinalIgnoreCase) ||
      query.Contains("by {url}", StringComparison.OrdinalIgnoreCase))
    errors.Add($"{path}: query groups by a prohibited high-cardinality or identifying dimension");
}

static void ValidateTabs(Dashboard dashboard, HashSet<long> ids, List<string> errors) {
  if (dashboard.Tabs == null || dashboard.Tabs.Count <= 0) return;
  var assigned = new HashSet<long>();
  foreach (Tab tab in dashboard.Tabs) {
    foreach (long id in tab.WidgetIds ?? new List<long>()) {
      if (!ids.Contains(id)) errors.Add($"tab '{tab.Name ?? "(unnamed)"}' references missing widget {id}");
      else if (!assigned.Add(id)) errors.Add($"widget {id} is assigned to more than one tab");
    }
  }
  var top = dashboard.Widgets?.Where(w => w.Id != null).Select(w => w.Id!.Value).ToHashSet() ?? new HashSet<long>();
  if (!assigned.SetEquals(top)) errors.Add("tabs must assign every top-level widget exactly once");
}

static bool IsLayout(string? layout) => layout is "ordered" or "free";

static bool IsType(string type) => type is
  "group" or "note" or "query_value" or "timeseries" or "query_table" or "toplist" or "sunburst" or
  "treemap" or "scatterplot" or "distribution" or "heatmap" or "geomap" or "list_stream";

public sealed class Dashboard {
  public string? Title { get; set; }
  [JsonPropertyName("layout_type")] public string? LayoutType { get; set; }
  public List<Widget>? Widgets { get; set; }
  public List<Tab>? Tabs { get; set; }
}

public sealed class Widget {
  public long? Id { get; set; }
  public Definition? Definition { get; set; }
  public Layout? Layout { get; set; }
}

public sealed class Definition {
  public string? Type { get; set; }
  public string? Title { get; set; }
  [JsonPropertyName("layout_type")] public string? LayoutType { get; set; }
  public List<Widget>? Widgets { get; set; }
  public List<Request>? Requests { get; set; }
}

public sealed class Layout {
  public int X { get; set; }
  public int Y { get; set; }
  public int Width { get; set; }
  public int Height { get; set; }
}

public sealed class Request {
  public List<Query>? Queries { get; set; }
  public List<Formula>? Formulas { get; set; }
}

public sealed class Query {
  public string? Name { get; set; }
  [JsonPropertyName("data_source")] public string? DataSource { get; set; }
  [JsonPropertyName("query")] public string? Text { get; set; }
}

public sealed class Formula {
  [JsonPropertyName("formula")] public string? Text { get; set; }
}

public sealed class Tab {
  public string? Name { get; set; }
  [JsonPropertyName("widget_ids")] public List<long>? WidgetIds { get; set; }
}

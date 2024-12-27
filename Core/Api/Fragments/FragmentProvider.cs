#region

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using IZ.Core.Api.Types;
using IZ.Core.Contexts;
using IZ.Core.Observability.Logging;

#endregion

namespace IZ.Core.Api.Fragments;

public interface IFragmentProvider {
  public Fragment LoadRequired(string fragmentName);

  public Fragment LoadRequired(TuneObjectDescriptor desc, string? format);

  public void LoadDirectory(string dir);
}

public class FragmentProvider : IHaveLogger, IFragmentProvider {
  private static string _graphqlDir = "GraphQL";

  public Dictionary<string, Fragment> Fragments { get; } = new Dictionary<string, Fragment>();

  public ITuneLogger Log { get; }

  private ZApp _app;

  public FragmentProvider(ZApp app) {
    _app = app;
    Log = app.Log.ForContext(GetType());
  }

  public Fragment LoadRequired(string fragmentName) {
    if (Fragments.TryGetValue(fragmentName, out var ret)) return ret;
    throw new SystemException($"Missing Fragment: {fragmentName}");
  }

  public Fragment LoadRequired(TuneObjectDescriptor desc, string? format) =>
    LoadRequired(desc, format, new HashSet<string>());

  private Fragment LoadRequired(TuneObjectDescriptor desc, string? format, HashSet<string> breadcrumbs) {
    if (string.IsNullOrWhiteSpace(format)) format = null;
    string fragmentName = Fragment.GetName(desc, format);
    if (breadcrumbs.Add(fragmentName)) {
      Log.Debug("[FRAGMENT] check {path}...", fragmentName);
      string? contents = null;
      bool generate = _app.Env <= IZEnvironment.Development || !Fragments.ContainsKey(fragmentName);

      string? path = null;
      if (Directory.Exists(_graphqlDir)) {
        string typeDir = Path.Join(_graphqlDir, desc.TypeName);
        if (!Directory.Exists(typeDir)) Directory.CreateDirectory(typeDir);
        path = Path.Join(typeDir, fragmentName + ".graphql");
      } else {
#if !TUNE_UNITY
        Log.Warning("[FRAGMENT] no persistent directory at {dir}", _graphqlDir);
#endif
      }

      if (generate) {
        Log.Debug("[FRAGMENT] creating {desc} {format} at {path}...", desc, format, path);
        contents = GenerateFragmentContents(desc, format, breadcrumbs);
        if (path != null) File.WriteAllText(path, contents);
      } else if (path != null && File.Exists(path)) {
        contents = File.ReadAllText(path);
      } else {
        IZEnv.Log.Warning("[FRAGMENT] path does not exist for {name} in {dir}", fragmentName, _graphqlDir);
        contents = GenerateFragmentContents(desc, format, breadcrumbs);
      }
      Fragments[fragmentName] = new Fragment(desc, format, contents);
    }
    return Fragments[fragmentName];
  }

  private string GenerateFragmentContents(TuneObjectDescriptor desc, string? format, HashSet<string> breadcrumbs, string? name = null) {
    if (string.IsNullOrWhiteSpace(format)) format = null;
    name ??= Fragment.GetName(desc, format);
    List<string> ret = new List<string> {
      $"fragment {name} on {desc.TypeName} {{"
    };
    foreach (string fieldName in desc.FieldMap.Keys) {
      var fieldType = desc.FieldMap[fieldName];

      var childDesc = fieldType.FieldTypeDescriptor.ObjectDescriptor;

      IZEnv.Log.Verbose("[FIELD] {type} {fieldName} {format}", desc.TypeName, fieldName, fieldType.Formats);

      if (fieldType.Formats.Any()) {
        // If the field specifies a format, we restrict inclusion
        if (!fieldType.Formats.Contains(format)) continue;
      } else if (!childDesc.IsScalar) {
        // Complex objects are excluded if there's no explicit formatting field
        continue;
      }

      string invoke = $"  {fieldName}";
      if (!childDesc.IsScalar) invoke += $" {{ ...{Fragment.GetName(childDesc, format)} }}";
      ret.Add(invoke);

      // Make sure this child fragment exists!
      if (!childDesc.IsScalar && !breadcrumbs.Contains(Fragment.GetName(childDesc, format))) LoadRequired(childDesc, format, breadcrumbs);
    }
    ret.Add("}");
    return string.Join("\n", ret);
  }

  public void LoadDirectory(string dir) {
    _graphqlDir = dir;
    if (!Directory.Exists(_graphqlDir)) {
      Log.Warning("[FRAGMENT] directory '{dir}' does not exist", dir);
      return;
    }
    Log.Information("[FRAGMENT] loading files from {dir}", dir);
    TuneApi.EnsureSchema();
    string[] files = Directory.GetFiles(dir, "*.graphql", SearchOption.AllDirectories);
    List<string> dependencies = new List<string>();
    foreach (string fn in files) {
      string[] parts = fn.Split(dir).Last().Split("/")
        .Where(s => !string.IsNullOrWhiteSpace(s)).ToArray();
      if (parts.Length != 2) {
        IZEnv.Log.Warning("[FRAGMENT] unknown type {@parts}", parts.Select(p => p));
        continue;
      }
      string fragmentName = parts.Last().Split(".").First();
      List<string> names = parts.Last().Split(".").First().Split("_").ToList();
      string? format = null;
      if (names.Count > 1) {
        // if (Enum.TryParse(names.Last(), out format)) {
        format = names.Last();
        names.RemoveAt(names.Count - 1);
        // }
      }
      string typeName = string.Join("_", names);
      var desc = TuneObjectDescriptor.FindTuneObjectDescriptor(typeName);
      if (desc == null) {
        Log.Warning("[FRAGMENT] type {name} missing; cannot load {fn}", typeName, fn);
        continue;
      }

      string contents = File.ReadAllText(fn);
      IZEnv.Log.Debug("[FRAGMENT] {name}", fragmentName);
      Fragments[fragmentName] = new Fragment(desc, format, contents);
      dependencies.AddRange(Fragments[fragmentName].DependencyNames);
    }
    List<string> missingDependencies = dependencies.Distinct()
      .Where(d => !dependencies.Contains(d)).ToList();

    if (missingDependencies.Any()) {
      throw new SystemException($"[FRAGMENTS] missing dependencies: {string.Join(", ", missingDependencies)}");
    }
  }
}

#region

using System;
using System.Collections.Concurrent;
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

  public Fragment LoadRequired(IZContext context, ZObjectDescriptor desc, string? format);

  public void LoadDirectory(string dir);
}

public class FragmentProvider : IHaveLogger, IFragmentProvider {
  private static string _graphqlDir = "GraphQL";

  private readonly ZApp _app;

  private bool _generateContents = false;

  public IZTypeMap TypeMap => _typeMap ?? ZApi.TypeMap;
  private IZTypeMap? _typeMap;

  public FragmentProvider(ZApp app, IZTypeMap? typeMap = null) {
    _app = app;
    _typeMap = typeMap;
    Log = app.Log.ForContext(GetType());
    _generateContents = _app.Target == ZTarget.PublicApp || _app.Env <= ZEnvironment.Development;
  }

  public ConcurrentDictionary<string, Fragment> Fragments { get; }
    = new ConcurrentDictionary<string, Fragment>();

  public Fragment LoadRequired(string fragmentName) {
    if (Fragments.TryGetValue(fragmentName, out var ret)) return ret;
    throw new SystemException($"Missing Fragment: {fragmentName}");
  }

  public Fragment LoadRequired(IZContext context, ZObjectDescriptor desc, string? format) =>
    LoadRequired(context, desc, format, new HashSet<string>());

  public void GenerateSourceFiles(IZContext context, string dir) {
    bool wasGenerate = _generateContents;
    _generateContents = true;
    _graphqlDir = dir;
    if (Directory.Exists(_graphqlDir)) Directory.Delete(_graphqlDir, true);
    Directory.CreateDirectory(_graphqlDir);
    var types = TypeMap.ApiObjects.Values.ToList();
    foreach (var type in types) {
      if (type.IsScalar) continue;
      // Log.Information("[FRAGMENT] loading {type}", type);
      foreach (var format in type.ExpectedFormats) {
        LoadRequired(context, type, format, new HashSet<string>());
      }
    }
    _generateContents = wasGenerate;
  }

  public void LoadDirectory(string dir) {
    _graphqlDir = dir;
    if (!Directory.Exists(_graphqlDir)) {
      var di = Directory.CreateDirectory(_graphqlDir);
      if (!di.Exists) {
        Log.Warning("[FRAGMENT] directory '{dir}' does not exist", dir);
        return;
      }
    }
    Log.Information("[FRAGMENT] loading files from {dir}", dir);
    string[] files = Directory.GetFiles(dir, "*.graphql", SearchOption.AllDirectories);
    List<string> dependencies = new List<string>();
    foreach (string fn in files) {
      string[] parts = fn.Split(dir).Last().Split("/")
        .Where(s => !string.IsNullOrWhiteSpace(s)).ToArray();
      if (parts.Length != 2) {
        ZEnv.Log.Warning("[FRAGMENT] unknown type {@parts}", parts.Select(p => p));
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
      var desc = TypeMap.ApiObjects.GetValueOrDefault(typeName);
      if (desc == null) {
        Log.Warning("[FRAGMENT] type {name} missing; cannot load {fn}", typeName, fn);
        continue;
      }

      string contents = File.ReadAllText(fn);
      ZEnv.Log.Debug("[FRAGMENT] {name}", fragmentName);
      Fragments[fragmentName] = new Fragment(desc, format, contents);
      dependencies.AddRange(Fragments[fragmentName].DependencyNames);
    }
    List<string> missingDependencies = dependencies.Distinct()
      .Where(d => !dependencies.Contains(d)).ToList();

    if (missingDependencies.Any()) {
      throw new SystemException($"[FRAGMENTS] missing dependencies: {string.Join(", ", missingDependencies)}");
    }
  }

  public IZLogger Log { get; set; }

  private bool IsFragmentObject(ZObjectDescriptor desc) => !desc.IsScalar;

  private Fragment LoadRequired(IZContext context, ZObjectDescriptor desc, string? format, HashSet<string> breadcrumbs) {
    if (string.IsNullOrWhiteSpace(format)) format = null;
    if (!IsFragmentObject(desc)) throw new ArgumentException($"{desc.TypeName} is not a fragmented type");
    string fragmentName = Fragment.GetName(desc, format);
    if (breadcrumbs.Add(fragmentName)) {
      context.Log.Debug("[FRAGMENT] check {dir} {path}...", fragmentName);
      string? contents = null;
      bool generate = _generateContents || !Fragments.ContainsKey(fragmentName);

      string? path = null;
      if (Directory.Exists(_graphqlDir)) {
        string typeDir = Path.Join(_graphqlDir, desc.TypeName);
        if (!Directory.Exists(typeDir)) Directory.CreateDirectory(typeDir);
        path = Path.Join(typeDir, fragmentName + ".graphql");
      } else {
        context.Log.Debug("[FRAGMENT] no persistent directory at {dir}", _graphqlDir);
      }

      if (generate) {
        context.Log.Debug("[FRAGMENT] creating {desc} {format} at {path}...", desc, format, path);
        contents = GenerateFragmentContents(context, desc, format, breadcrumbs);
        if (path != null) File.WriteAllText(path, contents);
      } else if (path != null && File.Exists(path)) {
        contents = File.ReadAllText(path);
      } else {
        context.Log.Warning("[FRAGMENT] path does not exist for {name} in {dir}", fragmentName, _graphqlDir);
        contents = GenerateFragmentContents(context, desc, format, breadcrumbs);
      }
      Fragments[fragmentName] = new Fragment(desc, format, contents);
    }
    return Fragments[fragmentName];
  }

  private string GenerateFragmentContents(IZContext context, ZObjectDescriptor desc, string? format, HashSet<string> breadcrumbs, string? name = null) {
    if (string.IsNullOrWhiteSpace(format)) format = null;
    name ??= Fragment.GetName(desc, format);
    List<string> ret = new List<string> {
      $"fragment {name} on {desc.TypeName} {{"
    };
    var props = desc.GetPropertiesForFormat(format);
    foreach (var prop in props) {

      // context.Log.Information("[FIELD] {type}.{field}={ft}", desc.TypeName, fieldName, prop.FieldType);

      var childDesc = prop.FieldTypeDescriptor.ObjectDescriptor;
      // context.Log.Information("[FIELD] {type}.{field}", desc.TypeName, prop);

      string invoke = $"  {prop.FieldName}";
      if (!childDesc.IsScalar) invoke += $" {{ ...{Fragment.GetName(childDesc, format)} }}";
      ret.Add(invoke);

      // Make sure this child fragment exists!
      if (!childDesc.IsScalar && !breadcrumbs.Contains(Fragment.GetName(childDesc, format))) LoadRequired(context, childDesc, format, breadcrumbs);
    }
    ret.Add("}");
    return string.Join("\n", ret);
  }
}

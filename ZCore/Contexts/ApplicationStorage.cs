#region

using System;
using System.IO;
using IZ.Core.Assets;
using IZ.Core.Data.Attributes;
using IZ.Core.Utils;

#endregion

namespace IZ.Core.Contexts;

public class ApplicationStorage {

  private static string? _zDir;

  private readonly string _productName;

  public ApplicationStorage(string productName) {
    _productName = productName;
    UserDir = GetUserDir(null);
    TmpDir = GetTmpDir(null);
    Assets = GetAssets();
  }

  public ApplicationStorage(string productName, string? userDir = null, string? tmpDir = null, string? www = null) {
    _productName = productName;
    UserDir = GetUserDir(userDir);
    TmpDir = GetTmpDir(tmpDir);
    Assets = GetAssets();
    WwwRoot = ExpandPath(www);
  }

  public ApplicationStorage(string productName, string? userDir = null, IAssetProvider? assetDir = null, string? tmpDir = null, string? www = null) {
    _productName = productName;
    UserDir = GetUserDir(userDir);
    TmpDir = GetTmpDir(tmpDir);
    Assets = assetDir ?? GetAssets();
    WwwRoot = ExpandPath(www);
  }
  [ApiDocs("User save directory")]
  public string UserDir { get; }

  public string GraphQLDir => Path.Combine(UserDir, "GraphQL");

  [ApiDocs("Bundled asset directory (SVGs etc.)")]
  public IAssetProvider Assets { get; }

  [ApiDocs("Scratch/working directory, for unzipping/zipping files etc.")]
  public string TmpDir { get; }

  [ApiDocs("Server hosting directory, if applicable")]
  public string? WwwRoot { get; }

  private string GetUserDir(string? userDir) => string.IsNullOrEmpty(userDir) ? Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) : ExpandPath(userDir);

  private string GetTmpDir(string? tmpDir) => string.IsNullOrEmpty(tmpDir) ? Environment.GetEnvironmentVariable("TMP_DIR") ?? "/tmp" : ExpandPath(tmpDir);

  private IAssetProvider GetAssets() => new FileAssetProvider();
  private  string FindZDir() {
    if (_zDir != null) return _zDir;
    string? dir = Directory.GetCurrentDirectory();
    while (!File.Exists(Path.Combine(dir, $"{_productName}.sln"))) {
      var parent = Directory.GetParent(dir) ??
                   throw new SystemException($"{_productName} solution file not found in {Directory.GetCurrentDirectory()}");
      dir = parent.FullName;
    }
    return _zDir = dir;
  }

  private string ExpandPath(string? path) {
    var envVar = _productName.ToSnakeCase().ToUpperInvariant() + "_DIR";
    if (path == null || !path.Contains($"${{{envVar}}}")) return path ?? Directory.GetCurrentDirectory();
    string? dir = Environment.GetEnvironmentVariable(envVar);
    if (string.IsNullOrWhiteSpace(dir)) dir = FindZDir();
    return path.Replace($"${{{envVar}}}", dir);
  }
}

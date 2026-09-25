#region

using System;
using System.IO;
using IZ.Core.Assets;
using IZ.Core.Data;
using IZ.Core.Data.Attributes;
using IZ.Core.Utils;

#endregion

namespace IZ.Core.Contexts;

public class ApplicationStorage : TransientObject {

  private static string? _zDir;

  private readonly string _productName;

  public ApplicationStorage(string productName) {
    _productName = productName;
    UserDir = GetUserDir(null);
    TmpDir = GetTmpDir(null);
    Assets = Bind(null);
  }

  public ApplicationStorage(string productName, string? userDir = null, string? tmpDir = null, string? www = null) {
    _productName = productName;
    UserDir = GetUserDir(userDir);
    TmpDir = GetTmpDir(tmpDir);
    Assets = Bind(null);
    WwwRoot = ExpandPath(www);
  }

  public ApplicationStorage(string productName, string? userDir = null, IAssetProvider? assetDir = null, string? tmpDir = null, string? www = null) {
    _productName = productName;
    UserDir = GetUserDir(userDir);
    TmpDir = GetTmpDir(tmpDir);
    Assets = Bind(assetDir);
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

  /// <summary>The provider resolves its asset directory from *this* storage's `UserDir`, never from the process-global
  /// `ZEnv.App`: two apps in one process (the test suite beside an in-process `CliApp`) used to race, and whichever was
  /// global when the provider first read `AssetDirectory` decided where every later asset write went.</summary>
  private IAssetProvider Bind(IAssetProvider? provider) {
    provider ??= new FileAssetProvider();
    if (provider is BaseAssetProvider owned) owned.AttachTo(this);
    return provider;
  }
  private string FindZDir() {
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
    string envVar = _productName.ToSnakeCase().ToUpperInvariant() + "_DIR";
    if (path == null || !path.Contains($"${{{envVar}}}")) return path ?? Directory.GetCurrentDirectory();
    string? dir = Environment.GetEnvironmentVariable(envVar);
    if (string.IsNullOrWhiteSpace(dir)) dir = FindZDir();
    return path.Replace($"${{{envVar}}}", dir);
  }
}

#region

using System;
using System.IO;
using IZ.Core.Assets;
using IZ.Core.Data.Attributes;

#endregion

namespace IZ.Core.Contexts;

public class ApplicationStorage {

  private static string? _tunealityDir;

  public ApplicationStorage() {
    UserDir = GetUserDir(null);
    TmpDir = GetTmpDir(null);
    Assets = GetAssets();
  }

  public ApplicationStorage(string? userDir = null, string? tmpDir = null, string? www = null) {
    UserDir = GetUserDir(userDir);
    TmpDir = GetTmpDir(tmpDir);
    Assets = GetAssets();
    WwwRoot = ExpandPath(www);
  }

  public ApplicationStorage(string? userDir = null, IAssetProvider? assetDir = null, string? tmpDir = null, string? www = null) {
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
  private static string FindTunealityDir() {
    if (_tunealityDir != null) return _tunealityDir;
    string? dir = Directory.GetCurrentDirectory();
    while (!File.Exists(Path.Combine(dir, "Tuneality.sln"))) {
      var parent = Directory.GetParent(dir) ??
                   throw new SystemException($"Tuneality not found in {Directory.GetCurrentDirectory()}");
      dir = parent.FullName;
    }
    return _tunealityDir = dir;
  }

  private string ExpandPath(string? path) {
    if (path == null || !path.Contains("${TUNEALITY_DIR}")) return path ?? Directory.GetCurrentDirectory();
    string? dir = Environment.GetEnvironmentVariable("TUNEALITY_DIR");
    if (string.IsNullOrWhiteSpace(dir)) dir = FindTunealityDir();
    return path.Replace("${TUNEALITY_DIR}", dir);
  }
}

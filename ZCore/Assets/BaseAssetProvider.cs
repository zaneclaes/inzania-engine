using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using IZ.Core.Contexts;
using IZ.Core.Utils;

namespace IZ.Core.Assets;

public abstract class BaseAssetProvider : LogicBase, IAssetProvider {

  private readonly HashSet<string> _activeDownloads = new HashSet<string>();
  private string? _assetDir;
  public abstract string Name { get; }

  public string AssetDirectory => _assetDir ??= LoadAssetDir();

  public string GetAssetPath(string relativePath) => Path.Combine(AssetDirectory, relativePath);

  public virtual async Task<byte[]?> GetAssetContents(string relativePath, string? downloadUrl = null, CancellationToken ct = new CancellationToken()) {
    string fp = Path.Combine(AssetDirectory, relativePath);
    bool exists = File.Exists(fp);
    if (!exists && downloadUrl != null && Context.App.Target != ZTarget.Server) {
      byte[] data = await GetUrl(downloadUrl);
      await File.WriteAllBytesAsync(fp, data, ct);
      return data;
    }

    return exists ? await File.ReadAllBytesAsync(fp, ct) : null;
  }

  public virtual byte[]? GetResourceContents(string relativePath) {
    string fn = GetAssetPath(relativePath);
    return File.Exists(fn) ? File.ReadAllBytes(fn) : null;
  }

  public async Task<string> CacheAsset(string relativePath, CancellationToken ct = new CancellationToken()) {
    string fp = GetAssetPath(relativePath);
    if (File.Exists(fp)) {
      Log.Debug("[ASSET] got cached {fp}", fp);
      return fp;
    }

    if (!_activeDownloads.Add(relativePath)) {
      await Tasks.WaitUntil(() => !_activeDownloads.Contains(relativePath), ct);
      if (!File.Exists(fp)) throw new NullReferenceException($"Asset does not exist at {fp}");
    } else {
      await DownloadAsset(relativePath, ct);
    }
    return fp;
  }

  private string LoadAssetDir() {
    string dir = FilePaths.GetAbsolutePath(Path.Combine(ZEnv.App.Storage.UserDir, "Assets"));
    if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
    return dir;
  }

  // Download a remote file directly and return the path for consumption
  private async Task DownloadAsset(string relativePath, CancellationToken ct = new CancellationToken()) {
    try {
      string fp = GetAssetPath(relativePath);
      Directory.CreateDirectory(Path.GetDirectoryName(fp)!);

      string unixPath = relativePath.Replace("\\\\", "/").Replace("\\", "/");
      string url = $"{Context.App.Cdn}/{unixPath}";
      Log.Information("[ASSET] download {url} to {fp}", url, fp);
      byte[] data = await GetAssetContents(relativePath, url, ct) ??
                    throw new NullReferenceException($"Failed to get contents from {url}");
      await File.WriteAllBytesAsync(fp, data, ct);
    } finally {
      _activeDownloads.Remove(relativePath);
    }
  }

  protected virtual async Task<byte[]> GetUrl(string url) {
    using var client = new HttpClient();
    // using var input = await client.GetStreamAsync(url);
    return await client.GetByteArrayAsync(url);
  }
}

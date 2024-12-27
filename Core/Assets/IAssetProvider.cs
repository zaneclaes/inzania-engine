#region

using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using IZ.Core.Contexts;

#endregion

namespace IZ.Core.Assets;

public interface IAssetProvider : IHaveContext {
  public string Name { get; }

  public string AssetDirectory { get; }

  public string GetAssetPath(string relativePath);

  public Task<byte[]?> GetAssetContents(string relativePath, string? downloadUrl = null, CancellationToken ct = new CancellationToken());

  public byte[]? GetResourceContents(string relativePath);

  // Download a remote file directly and return the path for consumption
  public async Task<string> DownloadAsset(string relativePath, CancellationToken ct = new CancellationToken()) {
    string fp = GetAssetPath(relativePath);
    if (File.Exists(fp)) {
      Log.Information("[ASSET] got cached {fp}", fp);
      return fp;
    }
    Directory.CreateDirectory(Path.GetDirectoryName(fp)!);

    var unixPath = relativePath.Replace("\\\\", "/").Replace("\\", "/");
    var url = $"{Context.App.Cdn}/downloads/{unixPath}";
    Log.Information("[ASSET] download {url} to {fp}", url, fp);
    var data = await GetAssetContents(relativePath, url, ct) ??
               throw new NullReferenceException($"Failed to get contents from {url}");
    await File.WriteAllBytesAsync(fp, data, ct);

    return fp;
  }

  public string? GetResourceText(string name, Encoding? enc = null) {
    byte[]? data = GetResourceContents(name);
    if (data == null) return null;
    enc ??= Encoding.UTF8;
    return enc.GetString(data);
  }

  public async Task<string?> GetAssetText(string name, string? downloadUrl = null, Encoding? enc = null) {
    byte[]? data = await GetAssetContents(name, downloadUrl);
    if (data == null) return null;
    enc ??= Encoding.UTF8;
    return enc.GetString(data);
  }
}

using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using IZ.Core.Contexts;
using IZ.Core.Utils;

namespace IZ.Core.Assets;

public abstract class BaseAssetProvider : LogicBase, IAssetProvider {
  public abstract string Name { get; }

  public string AssetDirectory => _assetDir ??= LoadAssetDir();
  private string? _assetDir;

  public string GetAssetPath(string relativePath) => Path.Combine(AssetDirectory, relativePath);

  private string LoadAssetDir() {
    var dir = FilePaths.GetAbsolutePath(Path.Combine(Context.App.Storage.UserDir, "Assets"));
    if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
    return dir;
  }

  public virtual async Task<byte[]?> GetAssetContents(string relativePath, string? downloadUrl = null, CancellationToken ct = new CancellationToken()) {
    string fp = Path.Combine(AssetDirectory, relativePath);
    bool exists = File.Exists(fp);
    if (!exists && downloadUrl != null && Context.App.Target != TuneTarget.Server) {
      var data = await GetUrl(downloadUrl);
      await File.WriteAllBytesAsync(fp, data, ct);
      return data;
    }

    return exists ? await File.ReadAllBytesAsync(fp, ct) : null;
  }

  public virtual byte[]? GetResourceContents(string relativePath) {
    string fn = GetAssetPath(relativePath);
    return File.Exists(fn) ? File.ReadAllBytes(fn) : null;
  }

  protected virtual async Task<byte[]> GetUrl(string url) {
    using var client = new HttpClient();
    // using var input = await client.GetStreamAsync(url);
    return await client.GetByteArrayAsync(url);
  }
}

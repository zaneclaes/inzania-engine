#region

using System.Text;
using System.Threading;
using System.Threading.Tasks;
using IZ.Core.Contexts;
using IZ.Core.Json;
using IZ.Core.Utils;

#endregion

namespace IZ.Core.Assets;

public interface IAssetProvider : IHaveContext {
  public string Name { get; }

  public string AssetDirectory { get; }

  public string ResourceDirectory { get; }

  public string GetAssetPath(string relativePath);

  public ZTask<byte[]> GetResourceFromServer(string resourcePath);

  public ZTask<byte[]?> GetAssetContents(string relativePath, string? downloadUrl = null, CancellationToken ct = new CancellationToken());

  public byte[]? GetResourceContents(string relativePath);

  public ZTask<string> CacheAsset(string relativePath, CancellationToken ct = new CancellationToken());

  public string? GetResourceText(string name, Encoding? enc = null) {
    byte[]? data = GetResourceContents(name);
    if (data == null) return null;
    enc ??= Encoding.UTF8;
    return enc.GetString(data);
  }

  public async ZTask<string?> GetAssetText(string name, string? downloadUrl = null, Encoding? enc = null) {
    byte[]? data = await GetAssetContents(name, downloadUrl);
    if (data == null) return null;
    enc ??= Encoding.UTF8;
    return enc.GetString(data);
  }

  public async ZTask<T> GetResourceFromServer<T>(string resourcePath) {
    var json = await GetResourceFromServer(resourcePath);
    var txt = Encoding.UTF8.GetString(json);
    Log.Debug("[RESOURCE] got {t}", txt);
    return ZJson.DeserializeObject<T>(Context, txt)!;
  }
}

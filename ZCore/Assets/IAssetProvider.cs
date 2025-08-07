#region

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

  public Task<string> CacheAsset(string relativePath, CancellationToken ct = new CancellationToken());

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

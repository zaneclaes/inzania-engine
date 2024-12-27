using System.Threading;
using System.Threading.Tasks;

namespace IZ.Core.Assets;

public class NullAssetProvider : BaseAssetProvider {
  public override string Name => "null";

  public override Task<byte[]?> GetAssetContents(string relativePath, string? downloadUrl = null, CancellationToken ct = new CancellationToken()) => Task.FromResult<byte[]?>(null);

  public override byte[]? GetResourceContents(string relativePath) => null;
}

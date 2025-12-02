using System.Threading;
using System.Threading.Tasks;
using IZ.Core.Utils;

namespace IZ.Core.Assets;

public class NullAssetProvider : BaseAssetProvider {
  public override string Name => "null";

  public override ZTask<byte[]?> GetAssetContents(string relativePath, string? downloadUrl = null, CancellationToken ct = new CancellationToken()) => ZTask<byte[]?>.FromResult(null);

  public override byte[]? GetResourceContents(string relativePath) => null;
}

#region

using System.IO;
using System.Threading;
using System.Threading.Tasks;
using HotChocolate.Types;
using IZ.Core.Api;

#endregion

namespace IZ.Schema.Types;

public class ZFileUpload : IFileUpload {
  private readonly IFile _file;

  public ZFileUpload(IFile file) {
    _file = file;
  }

  public string Name => _file.Name;
  public long? Length => _file.Length;
  public string? ContentType => _file.ContentType;
  public Task CopyToAsync(Stream target, CancellationToken cancellationToken = default) => _file.CopyToAsync(target, cancellationToken);
  public Stream OpenReadStream() => _file.OpenReadStream();
}

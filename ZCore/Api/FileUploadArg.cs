#region

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using IZ.Core.Data;

#endregion

namespace IZ.Core.Api;

public class FileUploadArg : TransientObject, IFileUpload {
  private readonly Func<Stream> _openReadStream;

  /// <summary>
  /// Creates a new instance of <see cref="StreamFile"/>.
  /// </summary>
  /// <param name="name">
  /// The file name.
  /// </param>
  /// <param name="openReadStream">
  /// A delegate to open the stream.
  /// </param>
  /// <param name="length">
  /// The file length if available.
  /// </param>
  /// <param name="contentType">
  /// The file content-type.
  /// </param>
  /// <exception cref="ArgumentException">
  /// <paramref name="name"/> is <c>null</c> or <see cref="string.Empty"/>.
  /// </exception>
  /// <exception cref="ArgumentNullException">
  /// <paramref name="openReadStream"/> is <c>null</c>.
  /// </exception>
  public FileUploadArg(
    string name,
    Func<Stream> openReadStream,
    long? length = null,
    string? contentType = null) {
    if (string.IsNullOrEmpty(name)) {
      throw new ArgumentException(nameof(FileUploadArg));
    }

    Name = name;
    _openReadStream = openReadStream ??
                      throw new ArgumentNullException(nameof(openReadStream));
    Length = length;
    ContentType = contentType;
  }

  public FileUploadArg(
    string name,
    byte[] contents,
    string? contentType = null) {
    Length = contents.Length;
    ContentType = contentType;
    var stream = new MemoryStream();
    stream.Write(contents, 0, contents.Length);
    Name = name;
    Contents = contents;
    _openReadStream = () => {
      stream.Seek(0, SeekOrigin.Begin);
      return stream;
    };
  }

  /// <inheritdoc />
  public string Name { get; }

  public byte[]? Contents { get; }

  /// <inheritdoc />
  public long? Length { get; }

  /// <inheritdoc />
  public string? ContentType { get; }

  /// <inheritdoc />
  public virtual async Task CopyToAsync(
    Stream target,
    CancellationToken cancellationToken = default) {
#if NETSTANDARD2_0 || NETSTANDARD2_1
        using var stream = OpenReadStream();
#else
    await using var stream = OpenReadStream();
#endif

#if NETSTANDARD2_0
        await stream.CopyToAsync(target, 1024, cancellationToken).ConfigureAwait(false);
#else
    await stream.CopyToAsync(target, cancellationToken).ConfigureAwait(false);
#endif
  }

  /// <inheritdoc />
  public virtual Stream OpenReadStream() => _openReadStream();
}

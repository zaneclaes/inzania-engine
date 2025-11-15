using System.IO;
using System.Threading;

#if Z_UNITY
using Cysharp.Threading.Tasks;
using ZTask = Cysharp.Threading.Tasks.UniTask;
using Tasks = Cysharp.Threading.Tasks.UniTask;
#else
using System.Threading.Tasks;
using ZTask = System.Threading.Tasks.Task;
#endif

namespace IZ.Core.Utils;

// Unity WebGL does not actually support File.*Async methods...
// This fakes them, to provide a usable option
public static class ZFile {
#if Z_UNITY
  public static UniTask<byte[]> ReadAllBytesAsync(string fp, CancellationToken cancellationToken = default) =>
    UniTask.FromResult(File.ReadAllBytes(fp));

  public static UniTask<string> ReadAllTextAsync(string fp, CancellationToken cancellationToken = default) =>
    UniTask.FromResult(File.ReadAllText(fp));

  public static UniTask WriteAllBytesAsync(string fp, byte[] bytes, CancellationToken cancellationToken = default) {
    File.WriteAllBytes(fp, bytes);
    return UniTask.CompletedTask;
  }

  public static UniTask WriteAllTextAsync(string fp, string text, CancellationToken cancellationToken = default) {
    File.WriteAllText(fp, text);
    return UniTask.CompletedTask;
  }
#else
  public static Task<byte[]> ReadAllBytesAsync(string fp, CancellationToken cancellationToken = default) =>
    File.ReadAllBytesAsync(fp, cancellationToken);

  public static Task<string> ReadAllTextAsync(string fp, CancellationToken cancellationToken = default) =>
    File.ReadAllTextAsync(fp, cancellationToken);

  public static Task WriteAllBytesAsync(string fp, byte[] bytes, CancellationToken cancellationToken = default) =>
    File.WriteAllBytesAsync(fp, bytes, cancellationToken);

  public static Task WriteAllTextAsync(string fp, string text, CancellationToken cancellationToken = default) =>
    File.WriteAllTextAsync(fp, text, cancellationToken);
#endif

}

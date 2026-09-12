#region

using System.Threading;
using System.Threading.Tasks;

#endregion

namespace IZ.Core.Data;

public static class DataRepositories {
  public static Task SaveIfNeededAsync(this IZDataRepository repo, CancellationToken ct = new CancellationToken()) =>
    repo.HasChanges ? repo.SaveAsync(ct) : Task.CompletedTask;

  /// <summary>
  /// The seed's save: nothing to do when nothing changed, and a row another replica inserted first is
  /// not a failure (<see cref="DataRepositoryBase.SaveTolerantAsync" />). Every seed goes through
  /// here; nothing else should.
  /// </summary>
  public static Task SaveSeedAsync(this IZDataRepository repo, CancellationToken ct = new CancellationToken()) {
    if (!repo.HasChanges) return Task.CompletedTask;
    return repo is DataRepositoryBase b ? b.SaveTolerantAsync(ct) : repo.SaveAsync(ct);
  }
}

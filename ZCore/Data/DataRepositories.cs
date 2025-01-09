#region

using System.Threading;
using System.Threading.Tasks;

#endregion

namespace IZ.Core.Data;

public static class DataRepositories {
  public static Task SaveIfNeededAsync(this IZDataRepository repo, CancellationToken ct = new CancellationToken()) =>
    repo.HasChanges ? repo.SaveAsync(ct) : Task.CompletedTask;
}

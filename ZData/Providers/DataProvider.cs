#region

using System;
using System.Diagnostics;
using System.Threading.Tasks;
using IZ.Core.Contexts;
using IZ.Core.Data.Seeds;
using IZ.Core.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

#endregion

namespace IZ.Data.Providers;

public static class DataProvider {
  /// <summary>Seconds a single migration statement may run (see MigrateDatabaseAsync).</summary>
  public const int MigrationCommandTimeout = 3600;

  public static async Task MigrateDatabaseAsync<TDb>(this IServiceProvider services) where TDb : DbContext {
    using var op = services.ScopeOperation();
    await op.ExecuteVoidTask(async () => {
      var db = op.ServiceProvider.GetRequiredService<TDb>();
      // A migration is not a query and must not inherit the query timeout. Index builds and
      // backfill UPDATEs on a large table legitimately run for minutes, and timing one out
      // mid-run is how a schema ends up half-applied — the failure mode that costs the most to
      // untangle. Restored afterwards so normal traffic keeps its own (short) budget.
      int? restore = db.Database.GetCommandTimeout();
      db.Database.SetCommandTimeout(MigrationCommandTimeout);
      try { await db.Database.MigrateAsync(); }
      finally { db.Database.SetCommandTimeout(restore); }
    });
  }

  public static async Task SeedDatabaseAsync(
    this IServiceProvider services, params IDataSeed[] seeds
  ) {
    using var op = services.ScopeOperation();
    await op.ExecuteVoidTask(async () => {
      foreach (var seed in seeds) {
        var sw = Stopwatch.StartNew();
        await seed.SeedDatabase(op);
        await op.Data.SaveAsync();
        op.Log.Information("[SEED] {type} ran in {ms}ms", seed.GetType().Name, sw.ElapsedMilliseconds);
      }
    });
  }
}

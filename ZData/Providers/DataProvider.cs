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
        // No save here, deliberately. A seed saves at the end of its own pass, inside its own
        // try/catch (`DataSeed.SeedDatabase`, through the tolerant seed save), so a save in this
        // loop body was redundant when the seed succeeded — and was the bug when it failed. The
        // per-seed catch exists so one seed's failure costs only that seed; but a bare `SaveAsync`
        // out here ran on a change tracker still holding the failed seed's `Added` entities, threw
        // the same exception a second time OUTSIDE that catch, unwound this `foreach`, and skipped
        // every remaining seed. That is how the production rollout of 2026-09-17 left a replica
        // with 5 of its 9 seeds after `FileSeed` lost a duplicate-key race.
        // If a seed ever needs a flush at this point — one that adds entities outside its own
        // `Exec()` — it must be the seed's own tolerant save AND inside the same protection as the
        // seed it belongs to. It may not be a bare save in this loop body.
        await seed.SeedDatabase(op);
        op.Log.Information("[SEED] {type} ran in {ms}ms", seed.GetType().Name, sw.ElapsedMilliseconds);
      }
    });
  }
}

#region

using System;
using System.Threading.Tasks;
using IZ.Core.Contexts;
using IZ.Core.Data;
using IZ.Core.Data.Seeds;
using IZ.Core.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

#endregion

namespace IZ.Data.Providers;

public static class DataProvider {
  public static async Task MigrateDatabaseAsync<TDb>(this IServiceProvider services) where TDb : DbContext {
    using var op = services.ScopeOperation();
    await op.ExecuteVoidTask(async () => {
      var db = op.ServiceProvider.GetRequiredService<TDb>();
      await db.Database.MigrateAsync();
    });
  }

  public static async Task SeedDatabaseAsync(
    this IServiceProvider services, params DataSeed[] seeds
  ) {
    using var op = services.ScopeOperation();
    await op.ExecuteVoidTask(async () => {
      foreach (var seed in seeds) {
        await seed.SeedDatabase(op);
      }
    });
  }
}

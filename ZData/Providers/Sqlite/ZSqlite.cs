#region

using System;
using IZ.Core.Data;
using IZ.Data.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

#endregion

namespace IZ.Data.Providers.Sqlite;

public static class ZSqlite {
  public static IServiceCollection AddZSqlite<TDc>(this IServiceCollection services, IConfigurationSection section) where TDc : ZDbContext {
    var settings = new SqliteSettings(section);
    return AddZSqlite<TDc>(services, settings.ToConnectionString(null));
  }

  public static void ConfigureSqlite<TDc>(IServiceProvider sp, SqliteDbContextOptionsBuilder opts) {
    opts.MigrationsAssembly(typeof(TDc).Assembly.FullName);
  }

  private static IServiceCollection AddZSqlite<TDc>(this IServiceCollection services, string connStr) where TDc : ZDbContext {
    return services
      .AddScoped<IZDataRepository, ZEfCoreDataRepository<TDc>>()
      .AddScoped<IZDataFactory, ZEfCoreDataFactory<TDc>>()
      .AddScoped<TDc>()
      .AddDbContext<TDc>((sp, opts) =>
        opts.UseSqlite(connStr, o => ConfigureSqlite<TDc>(sp, o)))
      ;
  }
}

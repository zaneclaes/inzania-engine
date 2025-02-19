#region

using IZ.Core.Data;
using IZ.Data.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

#endregion

namespace IZ.Server.Sql;

public static class ZMySql {
  public static IServiceCollection AddZMySql<TData>(this IServiceCollection services, MySqlOptions settings) where TData : ZDbContext {
    return services
      .AddScoped<IZDataRepository, ZEfCoreDataRepository<TData>>()
      .AddScoped<IZDataFactory, ZEfCoreDataFactory<TData>>()
      .AddScoped<ZDbContext, TData>()
      .AddScoped<TData>()
      .AddPooledDbContextFactory<TData>((sp, opts) =>
        opts.ConfigureMySql<TData>(settings));
  }

  public static DbContextOptionsBuilder ConfigureMySql<TAsm>(this DbContextOptionsBuilder options, MySqlOptions settings) {
    return options
      // .UseLazyLoadingProxies()
      .UseMySql(settings.ToConnectionString(options), settings.Version, opts => {
        opts.EnablePrimitiveCollectionsSupport();
        opts.TranslateParameterizedCollectionsToConstants();
        opts.MigrationsAssembly(typeof(TAsm).Assembly.FullName);
        opts.EnableRetryOnFailure(3);
        opts.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
      })
      .EnableSensitiveDataLogging() // gives column names in errors
      .ConfigureWarnings(w => {
        // For query splitting...
        w.Ignore(CoreEventId.RowLimitingOperationWithoutOrderByWarning);
      })
      .EnableDetailedErrors();
  }
}

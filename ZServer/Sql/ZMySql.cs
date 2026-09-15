#region

using IZ.Core.Data;
using IZ.Data.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

#endregion

namespace IZ.Server.Sql;

public static class ZMySql {
  public static IServiceCollection AddZMySql<TData>(this IServiceCollection services, MySqlOptions settings, bool logSensitiveData = false) where TData : ZDbContext {
    return services
      .AddScoped<IZDataRepository, ZEfCoreDataRepository<TData>>()
      .AddScoped<IZDataFactory, ZEfCoreDataFactory<TData>>()
      .AddScoped<ZDbContext, TData>()
      .AddScoped<TData>()
      .AddPooledDbContextFactory<TData>((sp, opts) =>
        opts.ConfigureMySql<TData>(settings, logSensitiveData));
  }

  /// <param name="logSensitiveData">EF puts parameter values (user rows, emails, tokens) into log entries and
  /// exception messages when this is on. Development and tests only: a deployed environment passes false.</param>
  public static DbContextOptionsBuilder ConfigureMySql<TAsm>(this DbContextOptionsBuilder options, MySqlOptions settings,
    bool logSensitiveData = false) {
    if (logSensitiveData) options.EnableSensitiveDataLogging();
    return options
      // .UseLazyLoadingProxies()
      .UseMySql(settings.ToConnectionString(options), settings.Version, opts => {
        opts.EnablePrimitiveCollectionsSupport();
        // Keep the existing stable SQL shape: collection values are translated as constants.
        opts.UseParameterizedCollectionMode(ParameterTranslationMode.Constant);
        opts.MigrationsAssembly(typeof(TAsm).Assembly.FullName);
        opts.EnableRetryOnFailure(3);
        opts.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
      })
      .ConfigureWarnings(w => {
        // For query splitting...
        w.Ignore(CoreEventId.RowLimitingOperationWithoutOrderByWarning);
      })
      .EnableDetailedErrors();
  }
}

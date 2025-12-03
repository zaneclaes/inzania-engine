using System;
using Microsoft.Extensions.DependencyInjection;

namespace IZ.Core.Utils;

public static class DiUtils {

  // Add a service where the alias actually references the original service, i.e., 2 different interfaces

  public static IServiceCollection AddTransients<TService, TImplementation>(this IServiceCollection services)
    where TService : class
    where TImplementation : class, TService => services
    .AddTransient<TImplementation>()
    .AddTransient<TService>(sp => sp.GetRequiredService<TImplementation>());

  public static IServiceCollection AddTransient<TService, TImplementation, TAlias>(this IServiceCollection services)
    where TService : class, TAlias
    where TImplementation : class, TService, TAlias
    where TAlias : class => services
    .AddTransients<TService, TImplementation>()
    .AddTransient<TAlias>(sp => sp.GetRequiredService<TService>());

  public static IServiceCollection AddScopeds<TService, TImplementation>(this IServiceCollection services)
    where TService : class
    where TImplementation : class, TService => services
    .AddScoped<TImplementation>()
    .AddScoped<TService>(sp => sp.GetRequiredService<TImplementation>());

  public static IServiceCollection AddScoped<TService, TImplementation, TAlias>(this IServiceCollection services)
    where TService : class, TAlias
    where TImplementation : class, TService, TAlias
    where TAlias : class => services
    .AddScopeds<TService, TImplementation>()
    .AddScoped<TAlias>(sp => sp.GetRequiredService<TService>());

  public static IServiceCollection AddSingletons<TService, TImplementation>(this IServiceCollection services, Func<TImplementation>? imp = null)
    where TService : class
    where TImplementation : class, TService => services
    .AddSingletonImplementation<TImplementation>(imp)
    .AddSingleton<TService>(sp => sp.GetRequiredService<TImplementation>());

  public static IServiceCollection AddSingletonImplementation<TImplementation>(this IServiceCollection services, Func<TImplementation>? imp = null) where TImplementation : class {
    if (imp == null) return services.AddSingleton<TImplementation>();
    else return services.AddSingleton(imp);
  }

  public static IServiceCollection AddSingletons<TService, TImp1, TImplementation>(this IServiceCollection services, Func<TImplementation>? imp = null)
    where TService : class
    where TImp1 : class, TService
    where TImplementation : class, TService, TImp1 => services
    .AddSingletonImplementation<TImplementation>(imp)
    .AddSingleton<TImp1>(sp => sp.GetRequiredService<TImplementation>())
    .AddSingleton<TService>(sp => sp.GetRequiredService<TImplementation>())
  ;

  public static IServiceCollection AddSingleton<TService, TImplementation, TAlias>(this IServiceCollection services)
    where TService : class, TAlias
    where TImplementation : class, TService, TAlias
    where TAlias : class => services
    .AddSingletons<TService, TImplementation>()
    .AddSingleton<TAlias>(sp => sp.GetRequiredService<TService>());
}

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

  public static IServiceCollection AddSingletons<TService, TImplementation>(this IServiceCollection services)
    where TService : class
    where TImplementation : class, TService => services
    .AddSingleton<TImplementation>()
    .AddSingleton<TService>(sp => sp.GetRequiredService<TImplementation>());

  public static IServiceCollection AddSingleton<TService, TImplementation, TAlias>(this IServiceCollection services)
    where TService : class, TAlias
    where TImplementation : class, TService, TAlias
    where TAlias : class => services
    .AddSingletons<TService, TImplementation>()
    .AddSingleton<TAlias>(sp => sp.GetRequiredService<TService>());
}

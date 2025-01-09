#region

using System;
using IZ.Core.Api.Fragments;
using IZ.Core.Contexts;
using Microsoft.Extensions.DependencyInjection;
using ExecutionContext = IZ.Core.Contexts.ExecutionContext;

#endregion

namespace IZ.Core.Utils;

public static class DependencyInjection {
  public static IServiceCollection AddTunealityApp<TApp, TRoot>(
    this IServiceCollection sc, TApp app, TRoot? rootSingleton = null
  ) where TApp : ZApp where TRoot : class, IZRootContext {
    if (rootSingleton != null) sc.AddSingleton<IZRootContext>(rootSingleton);
    else sc.AddTransient<IZRootContext, TRoot>();

    // Fallback data-cache
    // sc.TryAddTransient<ITuneDataRepository, DataCacheRepository>();

    return sc
        .AddSingleton(app.Log)
        .AddSingleton(app)
        .AddSingleton<ZApp>(app)
        .AddTransient<IZContext, ExecutionContext>()
        .AddTransient<IZBackgroundContext, WorkContext>()
        .AddTransient<IZChildContext, AutoContext>()
        // .AddSingleton<WorkReceiver>()
        .AddSingleton<IFragmentProvider>(app.Fragments)
      ;
  }

  public static OperationContext ScopeOperation(this IServiceProvider serviceProvider) {
    var scope = serviceProvider.CreateScope();
    var context = scope.ServiceProvider.GetRootContext();
    return new OperationContext(context, scope);
  }
}

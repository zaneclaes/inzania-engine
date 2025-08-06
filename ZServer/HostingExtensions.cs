#region

using System.Threading.Tasks;
using HotChocolate.Execution;
using HotChocolate.Execution.Configuration;
using HotChocolate.Subscriptions.Diagnostics;
using IZ.Core;
using IZ.Core.Auth;
using IZ.Core.Contexts;
using IZ.Core.Utils;
using IZ.Schema;
using IZ.Schema.Loaders;
using IZ.Server.Graphql;
using IZ.Server.Health;
using IZ.Server.Http;
using IZ.Server.WebSockets;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Serilog;
using StackExchange.Redis;
using RequestDelegate = HotChocolate.Execution.RequestDelegate;

#endregion

namespace IZ.Server;

public static class HostingExtensions {
  private static readonly FileExtensionContentTypeProvider _extTypes = new FileExtensionContentTypeProvider();

  public static IServiceCollection AddZServerCore<T>(
    this IServiceCollection collection, T zApp
  ) where T : ZApp => collection
    .AddZApp<T, HostContext>(zApp)
    .AddLogging(lb => lb.AddSerilog())
    .AddHttpContextAccessor()
    .AddTransient<IProvideRootContext, HttpRootContextAccessor>()
    .AddSerilog();

  public static IServiceCollection AddZServerHttp(this IServiceCollection collection, ZApp app) {
    return collection
      .Configure<StaticFileOptions>(opts => {
        opts.ServeUnknownFileTypes = true;
        if (app.Env > ZEnvironment.Development) opts.FileProvider = new PhysicalFileProvider(app.Storage.WwwRoot!);

        // var contentTypeProvider = new FileExtensionContentTypeProvider();
        // contentTypeProvider.Mappings[".wasm.gz"] = "application/wasm";
        // // contentTypeProvider.Mappings[".js.gz"] = "application/wasm";
        // opts.ContentTypeProvider = contentTypeProvider;

        opts.OnPrepareResponse = ctx => {
          // Only index.html is served, so cache busting still lets the CDN do its work.
          // ctx.Context.Response.Headers["Cache-Control"] = "no-cache";
          // When serving Unity locally
          ctx.Context.PrepareStaticFileHttpResponse();
        };
      })
      .AddExceptionHandler(a => {
        a.AllowStatusCode404Response = true;
        a.ExceptionHandlingPath = "/error";
      });
  }

  private static IRequestExecutorBuilder AddZSubscriptions(this IRequestExecutorBuilder collection) {
    // Redis / memory connection
    var redisCfg = System.Environment.GetEnvironmentVariable("REDIS_CONNECTION_STRING");
    if (!string.IsNullOrEmpty(redisCfg)) {
      collection = collection.AddRedisSubscriptions((sp) => ConnectionMultiplexer.Connect(redisCfg));
      Log.Information("[REDIS] {value}", redisCfg);
    } else {
      collection = collection.AddInMemorySubscriptions();
    }
    return collection
      .AddSubscriptionDiagnostics();
  }

  public static IServiceCollection AddZServerGraphQl<TAuth>(this IServiceCollection collection, ZApp app) where TAuth : class, IZAuthenticator => collection
    .AddTransient<IZAuthenticator, TAuth>()
    // .AddScoped<ISubscriptionDiagnosticEventsListener, ZSubscriptionDiagnostics>()
    .AddGraphQLServer()
    .ModifyRequestOptions(opt => opt.IncludeExceptionDetails = true)
    // .AddType<WorkMutation>()
    .AddSchemaQuery(app)
    .AddZSubscriptions()
    .AddAuthorization()
    // .UseRequest<DispatchMiddl>()
    .AddDiagnosticEventListener<ApiServerEventListener>()
    .AddHttpRequestInterceptor<ZHttpInterceptor>()
    .AddSocketSessionInterceptor<ZSocketInterceptor>()
    // .UseField<ZResolverMiddleware>()
    // .UseRequest(next => async (context) => {
    //   var c = context.Services.GetRequiredService<IZContext>();
    //   c.Log.Information("[NEXT] {c}", context);
    //   await next(context);
    // })
    .Services;

  public static void PrepareStaticFileHttpResponse(this HttpContext context) {
    string? path = context.Request.Path.Value?.ToLower() ?? "";
    bool isGzip = path.EndsWith(".gz");
    bool isBr = path.EndsWith(".br");
    if (isGzip) context.Response.Headers.ContentEncoding = "gzip";
    if (isBr) context.Response.Headers.ContentEncoding = "br";
    if (isGzip || isBr) {
      foreach (string ext in _extTypes.Mappings.Keys) {
        if (path.EndsWith($"{ext}.gz") || path.EndsWith($"{ext}.br")) {
          context.Response.Headers.ContentType = _extTypes.Mappings[ext];
          break;
        }
      }
    }
    context.Response.Headers.Append(ZHeaders.Env, context.RequestServices.GetRootContext().App.Env.ToString());
  }

  public static TOpts GetSectionOptions<TOpts>(this IConfiguration section, string name) where TOpts : new() {
    var ret = new TOpts();
    section.GetSection(name).Bind(name, ret);
    return ret;
  }

  public static IServiceCollection AddZServerHealthChecks(this IServiceCollection collection) => collection
    .AddHealthChecks()
    .AddCheck<ProcessHealth>("process", tags: new[] {
      "liveness"
    })
    .AddCheck<HostHealth>("host", tags: new[] {
      "readiness"
    })
    .Services;

  public static ApplicationStorage ToZApplicationDirectories(this IConfigurationSection dirs, string productName) {
    return new ApplicationStorage(
      productName,
      dirs.GetSection("User").Value!,
      dirs.GetSection("Tmp").Value!,
      dirs.GetSection("Www").Value);
  }
}

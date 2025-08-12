#region

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using HotChocolate.Execution.Configuration;
using IZ.Core;
using IZ.Core.Api.Types;
using IZ.Core.Auth;
using IZ.Core.Contexts;
using IZ.Core.Data;
using IZ.Core.Utils;
using IZ.Schema;
using IZ.Server.Graphql;
using IZ.Server.Health;
using IZ.Server.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Serilog;
using StackExchange.Redis;

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
    string? redisCfg = Environment.GetEnvironmentVariable("REDIS_CONNECTION_STRING");
    if (!string.IsNullOrEmpty(redisCfg)) {
      collection = collection.AddRedisSubscriptions(sp => ConnectionMultiplexer.Connect(redisCfg));
      Log.Information("[REDIS] {value}", redisCfg);
    } else {
      collection = collection.AddInMemorySubscriptions();
    }
    return collection
      .AddSubscriptionDiagnostics();
  }

  public static IServiceCollection AddZServerGraphQl(this IServiceCollection collection, ZApp app)  => collection
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

  public static ApplicationStorage ToZApplicationDirectories(this IConfigurationSection dirs, string productName) => new ApplicationStorage(
    productName,
    dirs.GetSection("User").Value!,
    dirs.GetSection("Tmp").Value!,
    dirs.GetSection("Www").Value);

  public static TObj ToZObject<TObj>(this IConfigurationSection section, IZContext context) where TObj : ApiObject =>
    (section.ToZObject(typeof(TObj), context) as TObj)!;

  private static object ToZObject(this IConfigurationSection section, Type t, IZContext context) {
    var desc = ZObjectDescriptor.LoadZObjectDescriptor(t);
    var obj = (Activator.CreateInstance(t) as ApiObject)!;
    obj.Context = context;
    foreach (var prop in desc.AllProperties) {
      if (!prop.IsSettable || prop.IsJsonIgnored || prop.FieldType.IsAssignableTo(typeof(IAmInternal))) continue;
      var key = prop.FieldName.ToTitleCase("");
      object? val = null;
      if (prop.FieldTypeDescriptor.IsList) {
        if (prop.FieldTypeDescriptor.ObjectDescriptor.IsScalar) {
          val = section.GetSection(key).Get(prop.FieldType)!;
        } else if (prop.FieldTypeDescriptor.ObjectDescriptor.ObjectType.IsAssignableTo(typeof(ApiObject))) {
          var list = (Activator.CreateInstance(prop.FieldType) as IList)!;
          var children = section.GetSection(key).GetChildren().ToArray();
          foreach (var child in children) {
            list.Add(child.ToZObject(prop.FieldTypeDescriptor.ObjectDescriptor.ObjectType, context));
          }
          val = list;
        } else {
          context.Log.Warning("[CFG] {prop} is not API object", prop.FieldName);
        }
      } else if (!prop.FieldTypeDescriptor.ObjectDescriptor.IsScalar) {
        if (!prop.FieldTypeDescriptor.ObjectDescriptor.ObjectType.IsAssignableTo(typeof(ApiObject))) {
          context.Log.Warning("[CFG] {prop} is not API object", prop.FieldName);
        } else {
          val = section.GetSection(key).ToZObject(prop.FieldTypeDescriptor.ObjectDescriptor.ObjectType, context);
        }
      } else {
        val = section.GetValue(prop.FieldType, key);
      }
      prop.SetValue(obj, val);
    }
    return obj;
  }

}

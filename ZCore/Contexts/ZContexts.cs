#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using IZ.Core.Api;
using IZ.Core.Auth;
using IZ.Core.Data;
using IZ.Core.Observability.Logging;
using IZ.Core.Utils;
using Microsoft.Extensions.DependencyInjection;

#endregion

namespace IZ.Core.Contexts;

public static class ZContexts {
  public static IZChildContext ScopeAction<T>(this IZContext context, string? reason = null) => context.ScopeAction(typeof(T), reason);

  // public static Dictionary<string, object> GetMetricTags(this IEventEnricher obj) =>
  //   obj.EventTags.ToDictionary(p => p.Key.Replace(".", "_"), p => p.Value);
  //.Select(p => $"{p.Key.Replace(".", "_")}:{p.Value}").ToArray();

  // public static Dictionary<string, object> GetMetricTags(this IZContext context) =>
  //   context.GetEventProperties()
  //     .ToDictionary(k => k.Key.Replace(".", "_").ToLower(), k => k.Value);

  public static TModel CreateModelStringId<TModel>(this IZContext context, string? id = null) where TModel : ModelId, new() =>
    CreateModelId<TModel, string>(context, id ?? ModelId.GenerateId());

  public static TModel CreateModelId<TModel, TKey>(this IZContext context, TKey id) where TModel : ModelKey<TKey>, new() => new TModel {
    Id = id,
    Context = context
  };

  private static string? CheckUserRole(IZIdentity? id, ZUserRole minRole, params string[] bypassIds) {
    if (id?.IZUser == null) return nameof(IZContext.CurrentIdentity);
    if (id.IZUser.Role >= minRole) return null;
    if (!bypassIds.Contains(id.IZUser.Id)) return id.IZUser.Role.ToString();
    return null;
  }

  public static Dictionary<string, object> GetEventProperties(this IZContext context) {
    Dictionary<string, object>? ret = new Dictionary<string, object> {
      ["Context.Type"] = context.GetType().Name,
      ["Context.Resource"] = context.Resource
    };
    if (context.Action != null) ret["Context.Action"] = context.Action;
    return ret;
  }

  public static IZUser RequireIZUser(this IZContext context) =>
    context.CurrentIdentity?.IZUser ?? throw new AccessViolationException("UserId not provided (and no current user)");

  public static string RequireUserId(this IZContext context, string? userId = null) =>
    userId ?? (context.CurrentIdentity?.IZUser?.Id ?? throw new AccessViolationException("UserId not provided (and no current user)"));

  public static string? GetOwnerId(this IOwned owned) {
    if (owned is IAmOwned own) return own.UserId;
    if (owned is IMightBeOwned o) return o.UserId;
    throw new SystemException($"{owned.GetType()} is IOwned, but not IAmOwned or IMightBeOwned");
  }

  public static string? CheckOwnershipException(this IOwned owned, IZIdentity? id, ZUserRole bypassRole = ZUserRole.Admin) {
    string? ownerId = owned.GetOwnerId();
    return ownerId == null ? CheckUserRole(id, bypassRole) : CheckUserRole(id, bypassRole, ownerId);
  }

  public static void EnsureOwnership(this IOwned owned, IZIdentity? id, ZUserRole bypassRole = ZUserRole.Admin) {
    string? exception = owned.CheckOwnershipException(id, bypassRole);
    if (exception != null) throw new UnauthorizedAccessException(exception);
  }

  public static IZRootContext? TryGetRootContext(this IServiceProvider serviceProvider) =>
    serviceProvider.GetService<IProvideRootContext>()?.GetRootContext(serviceProvider);

  public static IZRootContext GetRootContext(this IServiceProvider serviceProvider) {
    var context = serviceProvider.TryGetRootContext();
    if (context != null) return context;
    // context = ZEnv.SpawnRootContext();
    return serviceProvider.GetService<IZBackgroundContext>() ?? serviceProvider.GetRequiredService<IZRootContext>();
  }

  public static IZContext GetCurrentContext(this IServiceProvider serviceProvider) {
    return serviceProvider.GetRootContext();
    // if (context != null) return context;
    // context = serviceProvider.TryGetRootContext();
    // if (context != null) return context;
    // // Fall back on
  }

  private static readonly string ExecTag = "EXEC";

  // public static object?[] Guard(this IZContext context, params object?[] items) {
  //   if (!items.Any()) return items;
  //   Stopwatch sw = new Stopwatch();
  //   sw.Start();
  //   foreach (object? item in items) {
  //     if (item == null) continue;
  //     if (item is IList list) {
  //       bool enforced = false;
  //       foreach (var ch in list) {
  //         if (ch is ApiObject obj) {
  //           if (obj.EnforceContext(context)) enforced = true;
  //         }
  //         else context.Log.Warning("[GUARD] list has {type}", ch?.GetType());
  //       }
  //       if (enforced)
  //         context.Log.Information("[GUARD] List<{type}> => {context}", list.GetType().GenericTypeArguments.FirstOrDefault(), context);
  //     } else if (item.GetType().IsArray) {
  //
  //     } else if (item is ApiObject obj) {
  //       if (obj.EnforceContext(context)) {
  //         context.Log.Information("[GUARD] enforce {type} => {context}", obj.GetType(), context);
  //       }
  //     } else {
  //       context.Log.Warning("[GUARD] response is non-API type {type}", items.GetType());
  //     }
  //   }
  //   sw.Stop();
  //   if (sw.Elapsed.TotalMilliseconds >= 100) context.Log.Warning("[GUARD] {type} took {ms}ms", items.First()!.GetType(), sw.Elapsed.TotalMilliseconds);
  //   return items;
  // }

  public static TRes ExecuteRequired<TRes>(this IZContext context, Func<TRes> task) {
    try {
      return task();
    } catch (Exception e) {
      context.HandleException(e, ExecTag, typeof(TRes).Name);
      throw;
    }
  }

  public static TRes? ExecuteOptional<TRes>(this IZContext context, Func<TRes?> task) {
    try {
      return task();
    } catch (Exception e) {
      context.HandleException(e, ExecTag, typeof(TRes).Name);
      throw;
    }
  }

  public static void ExecuteVoid(this IZContext context, Action task) {
    try {
      task();
    } catch (Exception e) {
      context.HandleException(e, ExecTag, "void");
      throw;
    }
  }

  public static async Task<TRes> ExecuteRequiredTask<TRes>(this IZContext context, Func<Task<TRes>> task) {
    try {
      return await task();
    } catch (Exception e) {
      context.HandleException(e, ExecTag, typeof(TRes).Name);
      throw;
    }
  }

  public static async Task<TRes?> ExecuteOptionalTask<TRes>(this IZContext context, Func<Task<TRes?>> task) {
    try {
      return await task();
    } catch (Exception e) {
      context.HandleException(e, ExecTag, typeof(TRes).Name);
      throw;
    }
  }

  public static async Task ExecuteVoidTask(this IZContext context, Func<Task> task) {
    try {
      await task();
    } catch (Exception e) {
      context.HandleException(e, ExecTag, "void");
      throw;
    }
  }

  public static T GetRequiredService<T>(this IZContext context) where T : notnull {
    try {
      return context.ServiceProvider.GetRequiredService<T>();
    } catch (Exception ex) {
      context.Log.Error(ex, "[CTXT] failed to get service {type} on {context}", typeof(T), context);
      throw;
    }
  }

  public static T? GetService<T>(this IZContext context) {
    try {
      return context.ServiceProvider.GetService<T>();
    } catch (Exception ex) {
      context.Log.Error(ex, "[CTXT] failed to get service {type} on {context}", typeof(T), context);
      throw;
    }
  }

  public static IZQueryable<TData> QueryFor<TData>(
    this IZContext context, ResultSet? set = null
  ) where TData : DataObject => context.Data.QueryFor<TData>(context, set);

  public static IZQueryProvider GetQueryProvider<TData>(this IZContext context) where TData : DataObject
    => context.Data.QueryFor<TData>(context).QueryProvider; // we create a new Query to ensure it's attached to the PASSED context

  public static TReq BeginRequest<TReq>(this IZContext context) where TReq : ZRequestBase =>
    context.GetService<IApiRequestFactory>()?.CreateApiRequest<TReq>(context) ??
    (Activator.CreateInstance(typeof(TReq), context) as TReq)!;

  public static void Verbose(this IZLogger log, string template, params object?[] args) =>
    log.Write(ZEventLevel.Verbose, template, args);
  public static void Debug(this IZLogger log, string template, params object?[] args) =>
    log.Write(ZEventLevel.Debug, template, args);
  public static void Information(this IZLogger log, string template, params object?[] args) =>
    log.Write(ZEventLevel.Information, template, args);
  public static void Warning(this IZLogger log, string template, params object?[] args) =>
    log.Write(ZEventLevel.Warning, template, args);
  public static void Warning(this IZLogger log, Exception e, string template, params object?[] args) =>
    log.Write(ZEventLevel.Warning, e, template, args);
  public static void Error(this IZLogger log, string template, params object?[] args) =>
    log.Write(ZEventLevel.Error, template, args);
  public static void Error(this IZLogger log, Exception e, string template, params object?[] args) =>
    log.Write(ZEventLevel.Error, e, template, args);
  public static void Fatal(this IZLogger log, string template, params object?[] args) =>
    log.Write(ZEventLevel.Fatal, template, args);
}

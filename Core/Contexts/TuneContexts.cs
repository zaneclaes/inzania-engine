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

public static class TuneContexts {
  public static ITuneChildContext ScopeAction<T>(this ITuneContext context, string? reason = null) => context.ScopeAction(typeof(T), reason);

  // public static Dictionary<string, object> GetMetricTags(this IEventEnricher obj) =>
  //   obj.EventTags.ToDictionary(p => p.Key.Replace(".", "_"), p => p.Value);
  //.Select(p => $"{p.Key.Replace(".", "_")}:{p.Value}").ToArray();

  // public static Dictionary<string, object> GetMetricTags(this ITuneContext context) =>
  //   context.GetEventProperties()
  //     .ToDictionary(k => k.Key.Replace(".", "_").ToLower(), k => k.Value);

  public static TModel CreateModelStringId<TModel>(this ITuneContext context, string? id = null) where TModel : ModelId, new() =>
    CreateModelId<TModel, string>(context, id ?? ModelId.GenerateId());

  public static TModel CreateModelId<TModel, TKey>(this ITuneContext context, TKey id) where TModel : ModelKey<TKey>, new() => new TModel {
    Id = id,
    Context = context
  };

  private static string? CheckUserRole(ITuneIdentity? id, TuneUserRole minRole, params string[] bypassIds) {
    if (id?.IZUser == null) return nameof(ITuneContext.CurrentIdentity);
    if (id.IZUser.Role >= minRole) return null;
    if (!bypassIds.Contains(id.IZUser.Id)) return id.IZUser.Role.ToString();
    return null;
  }

  public static Dictionary<string, object> GetEventProperties(this ITuneContext context) {
    Dictionary<string, object>? ret = new Dictionary<string, object> {
      ["Context.Type"] = context.GetType().Name,
      ["Context.Resource"] = context.Resource
    };
    if (context.Action != null) ret["Context.Action"] = context.Action;
    return ret;
  }

  public static ITuneUser RequireIZUser(this ITuneContext context) =>
    context.CurrentIdentity?.IZUser ?? throw new AccessViolationException("UserId not provided (and no current user)");

  public static string RequireUserId(this ITuneContext context, string? userId = null) =>
    userId ?? (context.CurrentIdentity?.IZUser?.Id ?? throw new AccessViolationException("UserId not provided (and no current user)"));

  public static string? GetOwnerId(this IOwned owned) {
    if (owned is IAmOwned own) return own.UserId;
    if (owned is IMightBeOwned o) return o.UserId;
    throw new SystemException($"{owned.GetType()} is IOwned, but not IAmOwned or IMightBeOwned");
  }

  public static string? CheckOwnershipException(this IOwned owned, ITuneIdentity? id, TuneUserRole bypassRole = TuneUserRole.Admin) {
    string? ownerId = owned.GetOwnerId();
    return ownerId == null ? CheckUserRole(id, bypassRole) : CheckUserRole(id, bypassRole, ownerId);
  }

  public static void EnsureOwnership(this IOwned owned, ITuneIdentity? id, TuneUserRole bypassRole = TuneUserRole.Admin) {
    string? exception = owned.CheckOwnershipException(id, bypassRole);
    if (exception != null) throw new UnauthorizedAccessException(exception);
  }

  public static ITuneRootContext? TryGetRootContext(this IServiceProvider serviceProvider) =>
    serviceProvider.GetService<IProvideRootContext>()?.GetRootContext(serviceProvider);

  public static ITuneRootContext GetRootContext(this IServiceProvider serviceProvider) {
    var context = serviceProvider.TryGetRootContext();
    if (context != null) return context;
    // context = IZEnv.SpawnRootContext();
    return serviceProvider.GetService<ITuneBackgroundContext>() ?? serviceProvider.GetRequiredService<ITuneRootContext>();
  }

  public static ITuneContext GetCurrentContext(this IServiceProvider serviceProvider) {
    return serviceProvider.GetRootContext();
    // ITuneContext? context = serviceProvider.GetService<ITuneChildContext>();
    // if (context != null) return context;
    // context = serviceProvider.TryGetRootContext();
    // if (context != null) return context;
    // // Fall back on
  }

  private static readonly string ExecTag = "EXEC";

  // public static object?[] Guard(this ITuneContext context, params object?[] items) {
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

  public static TRes ExecuteRequired<TRes>(this ITuneContext context, Func<TRes> task) {
    try {
      return task();
    } catch (Exception e) {
      context.HandleException(e, ExecTag, typeof(TRes).Name);
      throw;
    }
  }

  public static TRes? ExecuteOptional<TRes>(this ITuneContext context, Func<TRes?> task) {
    try {
      return task();
    } catch (Exception e) {
      context.HandleException(e, ExecTag, typeof(TRes).Name);
      throw;
    }
  }

  public static void ExecuteVoid(this ITuneContext context, Action task) {
    try {
      task();
    } catch (Exception e) {
      context.HandleException(e, ExecTag, "void");
      throw;
    }
  }

  public static async Task<TRes> ExecuteRequiredTask<TRes>(this ITuneContext context, Func<Task<TRes>> task) {
    try {
      return await task();
    } catch (Exception e) {
      context.HandleException(e, ExecTag, typeof(TRes).Name);
      throw;
    }
  }

  public static async Task<TRes?> ExecuteOptionalTask<TRes>(this ITuneContext context, Func<Task<TRes?>> task) {
    try {
      return await task();
    } catch (Exception e) {
      context.HandleException(e, ExecTag, typeof(TRes).Name);
      throw;
    }
  }

  public static async Task ExecuteVoidTask(this ITuneContext context, Func<Task> task) {
    try {
      await task();
    } catch (Exception e) {
      context.HandleException(e, ExecTag, "void");
      throw;
    }
  }

  public static ITuneQueryable<TData> QueryFor<TData>(
    this ITuneContext context, ResultSet? set = null
  ) where TData : DataObject => context.Data.QueryFor<TData>(context, set);

  public static ITuneQueryProvider GetQueryProvider<TData>(this ITuneContext context) where TData : DataObject
    => context.Data.QueryFor<TData>(context).QueryProvider; // we create a new Query to ensure it's attached to the PASSED context

  public static TReq BeginRequest<TReq>(this ITuneContext context) where TReq : TuneRequestBase =>
    context.ServiceProvider.GetService<IApiRequestFactory>()?.CreateApiRequest<TReq>(context) ??
    (Activator.CreateInstance(typeof(TReq), context) as TReq)!;

  public static void Verbose(this ITuneLogger log, string template, params object?[] args) =>
    log.Write(TuneEventLevel.Verbose, template, args);
  public static void Debug(this ITuneLogger log, string template, params object?[] args) =>
    log.Write(TuneEventLevel.Debug, template, args);
  public static void Information(this ITuneLogger log, string template, params object?[] args) =>
    log.Write(TuneEventLevel.Information, template, args);
  public static void Warning(this ITuneLogger log, string template, params object?[] args) =>
    log.Write(TuneEventLevel.Warning, template, args);
  public static void Warning(this ITuneLogger log, Exception e, string template, params object?[] args) =>
    log.Write(TuneEventLevel.Warning, e, template, args);
  public static void Error(this ITuneLogger log, string template, params object?[] args) =>
    log.Write(TuneEventLevel.Error, template, args);
  public static void Error(this ITuneLogger log, Exception e, string template, params object?[] args) =>
    log.Write(TuneEventLevel.Error, e, template, args);
  public static void Fatal(this ITuneLogger log, string template, params object?[] args) =>
    log.Write(TuneEventLevel.Fatal, template, args);
}

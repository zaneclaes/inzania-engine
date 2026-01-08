#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using IZ.Core.Api.GraphQLWebSockets;
using IZ.Core.Contexts;
using IZ.Core.Data;
using IZ.Core.Observability.Logging;
using IZ.Core.Utils;

#endregion

namespace IZ.Core.Api;

public interface IZResult {
  // Short, always refresh if possible when online
  public static TimeSpan DefaultOnlineCacheAge { get; set; } = TimeSpan.FromSeconds(1);

  // Long, offline allows big cache time (TODO)
  public static TimeSpan DefaultOfflineCacheAge { get; set; } = TimeSpan.FromDays(30);

  public ZTask<object> ExecuteObject(ResultSet? selectionSet = null);
}

public interface IZResult<TData> : IZResult where TData : class {
  public ZTask<TData> ExecuteData(ResultSet? selectionSet = null);

  public ZTask<IGraphQlWebSocket<TData>> Subscribe(IGraphQLWebSocketDelegate<TData> del, ResultSet? selectionSet = null);
}

public static class ZResultExtensions {

  public static ZTask<TData> Execute<TData>(
    this IZResult<TData> result, string? format = null
  ) where TData : class =>
    result.ExecuteData(new ResultSet {
      Format = format
    });

  public static ZTask<TData> Cache<TData>(
    this IZResult<TData> result, string? format = null, TimeSpan? maxCacheAge = null
  ) where TData : class =>
    result.ExecuteData(new ResultSet {
      Format = format,
      MaxCacheAge = maxCacheAge ?? IZResult.DefaultOnlineCacheAge
    });

  public static ZTask<IGraphQlWebSocket<TData>> Subscribe<TData>(
    this IZResult<TData> result, IGraphQLWebSocketDelegate<TData> del, string? format = null
  ) where TData : class =>
    result.Subscribe(del, new ResultSet {
      Format = format
    });
}

public class ZResult<TData> : TransientObject, IZResult<TData> where TData : class {

  private readonly Func<IExecutionPlan, TData>? _data;

  private readonly Func<IExecutionPlan, Task<TData>>? _task;

  public ZResult(IZContext context, Type parentClass, string name, Func<IExecutionPlan, TData> data, params object?[] args) : base(context) {
    _data = data;
    Args = args.ToList();
    MethodName = name;
    ParentClass = parentClass;
  }

  public ZResult(IZContext context, Type parentClass, string name, Func<IExecutionPlan, Task<TData>> dataTask, params object?[] args) : base(context) {
    _task = dataTask;
    Args = args.ToList();
    MethodName = name;
    ParentClass = parentClass;
  }

  public List<object?> Args { get; }

  public string MethodName { get; }

  public Type ParentClass { get; }

  public async ZTask<TData> ExecuteData(ResultSet? selectionSet = null) {
    selectionSet ??= new ResultSet();
    var plan = ExecutionPlan.Load(Context, ParentClass, MethodName, selectionSet);
    var serverConnection = Context.GetService<IServerConnection>();
    if (serverConnection != null) {
      var result = new ExecutionResult(Context, plan, Args);
      return await Context.ExecuteRequiredTask(async () => {
        var cache = selectionSet.MaxCacheAge == null ? null : Context.GetService<IZClientCache>();
        var res = cache?.Get<TData>(result.CacheId, selectionSet.MaxCacheAge);
        if (res != null) return res;
        res = await serverConnection.ExecuteApiRequest<TData>(result);
        if (cache != null) cache.Set(result.CacheId, res, selectionSet.Format);
        return res;
      });
    }
    // On the server, tasks log errors as Debug because they will be caught by GraphQL handlers
    var ret = _data != null ?
      Context.ExecuteRequired(() => _data(plan), ZEventLevel.Debug) :
      await Context.ExecuteRequiredTask(() => _task!(plan), ZEventLevel.Debug);
    try {
      await Context.Data.SaveAsync();
    } catch (Exception e) {
      Log.Error(e, "Executing {name}, failed to SaveIfNeededAsync", MethodName);
    }
    return ret;
  }

  public async ZTask<IGraphQlWebSocket<TData>> Subscribe(IGraphQLWebSocketDelegate<TData> del, ResultSet? selectionSet = null) {
    var plan = ExecutionPlan.Load(Context, ParentClass, MethodName, selectionSet ?? new ResultSet());
    var serverConnection = Context.GetRequiredService<IServerConnection>();
    var result = new ExecutionResult(Context, plan, Args);
    return await Context.ExecuteRequiredTask(() => serverConnection.Subscribe<TData>(result, del));
  }

  public async ZTask<object> ExecuteObject(ResultSet? selectionSet = null) => (await ExecuteData(selectionSet))!;
}

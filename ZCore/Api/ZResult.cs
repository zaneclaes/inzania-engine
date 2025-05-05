#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using IZ.Core.Api.GraphQLWebSockets;
using IZ.Core.Contexts;
using IZ.Core.Data;
using IZ.Core.Observability.Logging;

#endregion

namespace IZ.Core.Api;

public interface IZResult {
  public Task<object> ExecuteObject(ResultSet? selectionSet = null);
}

public interface IZResult<TData> : IZResult where TData : class {
  public Task<TData> ExecuteData(ResultSet? selectionSet = null);

  public Task<IGraphQlWebSocket<TData>> Subscribe(IGraphQLWebSocketDelegate<TData> del, ResultSet? selectionSet = null);
}

public class ZResult<TData> : TransientObject, IZResult<TData> where TData : class {
  private readonly Func<ExecutionPlan, TData>? _data;

  public List<object?> Args { get; }

  public string MethodName { get; }

  public Type ParentClass { get; }

  public ZResult(IZContext context, Type parentClass, string name, Func<ExecutionPlan, TData> data, params object?[] args) : base(context) {
    _data = data;
    Args = args.ToList();
    MethodName = name;
    ParentClass = parentClass;
  }

  private readonly Func<ExecutionPlan, Task<TData>>? _task;

  public ZResult(IZContext context, Type parentClass, string name, Func<ExecutionPlan, Task<TData>> dataTask, params object?[] args) : base(context) {
    _task = dataTask;
    Args = args.ToList();
    MethodName = name;
    ParentClass = parentClass;
  }

  public async Task<TData> ExecuteData(ResultSet? selectionSet = null) {
    var plan = ExecutionPlan.Load(Context, ParentClass, MethodName, selectionSet ?? new ResultSet());
    var serverConnection = Context.GetService<IServerConnection>();
    if (serverConnection != null) {
      var result = new ExecutionResult(Context, plan, Args);
      return await Context.ExecuteRequiredTask(() => serverConnection.ExecuteApiRequest<TData>(result));
    }
    // On the server, tasks log errors as Debug because they will be caught by GraphQL handlers
    var ret = _data != null ?
      Context.ExecuteRequired(() => _data(plan), ZEventLevel.Debug) :
      await Context.ExecuteRequiredTask(() => _task!(plan), ZEventLevel.Debug);
    try {
      await Context.Data.SaveAsync();
    } catch (Exception e) {
      Log.Error(e, "Executing {name}, failed to SaveIfNeededAsync",  MethodName);
    }
    return ret;
  }

  public async Task<IGraphQlWebSocket<TData>> Subscribe(IGraphQLWebSocketDelegate<TData> del, ResultSet? selectionSet = null) {
    var plan = ExecutionPlan.Load(Context, ParentClass, MethodName, selectionSet ?? new ResultSet());
    var serverConnection = Context.GetRequiredService<IServerConnection>();
    var result = new ExecutionResult(Context, plan, Args);
    return await Context.ExecuteRequiredTask(() => serverConnection.Subscribe<TData>(result, del));
  }

  public async Task<object> ExecuteObject(ResultSet? selectionSet = null) => (await ExecuteData(selectionSet))!;
}

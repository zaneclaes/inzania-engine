#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using IZ.Core.Contexts;
using IZ.Core.Data;
using Microsoft.Extensions.DependencyInjection;

#endregion

namespace IZ.Core.Api;

public interface IZResult {
  public Task<object> ExecuteObject(ResultSet? selectionSet = null);
}

public interface IZResult<TData> : IZResult {
  public Task<TData> ExecuteData(ResultSet? selectionSet = null);
}

public class ZResult<TData> : TransientObject, IZResult<TData> {
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
    var ret = _data != null ?
      Context.ExecuteRequired(() => _data(plan)) :
      await Context.ExecuteRequiredTask(() => _task!(plan));
    await Context.Data.SaveIfNeededAsync();
    return ret;
  }

  public async Task<object> ExecuteObject(ResultSet? selectionSet = null) => (await ExecuteData(selectionSet))!;
}

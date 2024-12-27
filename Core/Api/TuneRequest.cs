#region

using System;
using System.Threading.Tasks;
using IZ.Core.Contexts;

#endregion

namespace IZ.Core.Api;
/*
public interface ITuneRequest : IHaveContext {
  internal ITuneResult<TData> Result<TData>(
    Type parentClass, string name, Func<ExecutionPlan, TData> action, params object?[] args
  ) => new TuneResult<TData>(Context, parentClass, name, action, args);

  internal ITuneResult<TData> Result<TData>(
    Type parentClass, string name, Func<ExecutionPlan, Task<TData>> action, params object?[] args
  ) => new TuneResult<TData>(Context, parentClass, name, action, args);
}*/

public abstract class TuneRequestBase : LogicBase {
  // (ITuneServerConnection?) ServiceProvider.GetService(typeof(ITuneServerConnection))

  // private ITuneRequest Request { get; }

  protected TuneRequestBase(ITuneContext context) : base(context) {
    // Request = context;
  }

  protected ITuneResult<TData> Result<TData>(string name, Func<ExecutionPlan, TData> action, params object?[] args) =>
    new TuneResult<TData>(Context, GetType(), name, action, args);
    // Request.Result(GetType(), name, action, args);

  protected ITuneResult<TData> Result<TData>(string name, Func<ExecutionPlan, Task<TData>> action, params object?[] args) =>
    new TuneResult<TData>(Context, GetType(), name, action, args);
  //Request.Result(GetType(), name, action, args);
}

#region

using System;
using System.Collections.Generic;
using System.Linq;

#endregion

namespace IZ.Core.Contexts;

public interface IPoolable {
  public void EnterPool();

  public void ExitPool();
}

public interface IPool {
  public object ClaimObject();

  public void FreeObject(object obj);

  public List<object> GetBusyObjects();

  public List<T> GetBusy<T>() where T : IPoolable => GetBusyObjects().Where(o => o is T).Cast<T>().ToList();
}

public interface IPool<T> : IPool where T : IPoolable {
  public List<T> GetBusy() => (this).GetBusy<T>();
}

public abstract class FreeObjectPool : LogicBase, IPool {

  private readonly List<object> _free = new List<object>();

  private readonly List<object> _busy = new List<object>();

  public List<object> GetBusyObjects() => _busy;

  public FreeObjectPool(ITuneContext? context) : base(context) { }
  public abstract Type ObjectType { get; }

  protected abstract object CreateObject();

  protected virtual object ClaimFreeObject() {
    object? ret = _free.First();
    _free.RemoveAt(0);
    return ret;
  }

  public virtual object ClaimObject() {
    object claimed = _free.Any() ? ClaimFreeObject() : CreateObject();
    _busy.Add(claimed);
    // Log.Information("[POOL] busy {name}", claimed);
    return claimed;
  }

  public virtual void FreeObject(object obj) {
    if (obj.GetType() != ObjectType) throw new ArgumentException($"{obj.GetType()} / {ObjectType}");
    _free.Add(obj);
    _busy.Remove(obj);
    // Log.Information("[POOL] free {name}", obj);
  }
}

public abstract class FreeObjectPool<TObj> : FreeObjectPool, IPool<TObj> where TObj : IPoolable {
  protected FreeObjectPool(ITuneContext? context) : base(context) { }
  public override Type ObjectType => typeof(TObj);

  protected override object CreateObject() => Create()!;

  protected abstract TObj Create();

  public TObj Claim() => (TObj) ClaimObject();

  public void Free(TObj? obj) {
    if (obj != null) FreeObject(obj);
  }
}

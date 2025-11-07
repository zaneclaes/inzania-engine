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
  public List<T> GetBusy() => this.GetBusy<T>();
}

public abstract class FreeObjectPool : LogicBase, IPool {

  private readonly List<object> _busy = new List<object>();

  private readonly List<object> _free = new List<object>();

  public FreeObjectPool(IZContext? context) : base(context) { }
  public abstract Type ObjectType { get; }

  public List<object> GetBusyObjects() => _busy;

  public virtual object ClaimObject() {
    object claimed = ClaimFreeObject() ?? CreateObject();
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

  protected abstract object CreateObject();

  protected virtual object? ClaimFreeObject() {
    object? ret = _free.FirstOrDefault();
    if (ret == null) return null;
    _free.RemoveAt(0);
    return ret;
  }
}

public abstract class FreeObjectPool<TObj> : FreeObjectPool, IPool<TObj> where TObj : IPoolable {
  protected FreeObjectPool(IZContext? context) : base(context) { }
  public override Type ObjectType => typeof(TObj);

  protected override object CreateObject() => Create()!;

  protected abstract TObj Create();

  public TObj Claim() => (TObj) ClaimObject();

  public void Free(TObj? obj) {
    if (obj != null) FreeObject(obj);
  }
}

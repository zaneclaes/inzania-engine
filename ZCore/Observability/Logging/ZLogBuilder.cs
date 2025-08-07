using System;

namespace IZ.Core.Observability.Logging;

public abstract class ZLogBuilder : IDisposable {

  public abstract void Dispose();
  public abstract ZLogBuilder TransformObject<TObj>(Func<TObj, object> func);

  public abstract ZLogBuilder TransformObjectWhere<TObj>(Func<Type, bool> pred, Func<TObj, object> func);

  public abstract ZLogBuilder WriteToConsole();

  public abstract IZLogger BuildToSingleton();

  public abstract IZLogger Build();
}

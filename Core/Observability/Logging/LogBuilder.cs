using System;

namespace IZ.Core.Observability.Logging;

public abstract class LogBuilder : IDisposable {
  public abstract LogBuilder TransformObject<TObj>(Func<TObj, object> func);

  public abstract LogBuilder TransformObjectWhere<TObj>(Func<Type, bool> pred, Func<TObj, object> func);

  public abstract LogBuilder WriteToConsole();

  public abstract ITuneLogger BuildToSingleton();

  public abstract void Dispose();
}

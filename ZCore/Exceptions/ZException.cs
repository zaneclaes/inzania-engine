#region

using System;
using IZ.Core.Contexts;
using IZ.Core.Observability.Logging;

#endregion

namespace IZ.Core.Exceptions;

public abstract class ZException : Exception, IDisposable, IHaveContext {

  public ZException(IZContext context, string message, Exception? innerException = null) : base(message, innerException) {
    Context = context;
    Log = context.Log;
  }
  public IZContext Context { get; }
  public IZLogger Log { get; }

  public void Dispose() {
    // Context.Dispose();
  }
}

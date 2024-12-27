#region

using System;
using IZ.Core.Contexts;
using IZ.Core.Observability.Logging;

#endregion

namespace IZ.Core.Exceptions;

public abstract class TuneException : Exception, IDisposable, IHaveContext {

  public TuneException(ITuneContext context, string message, Exception? innerException = null) : base(message, innerException) {
    Context = context;
    Log = context.Log;
  }
  public ITuneContext Context { get; }
  public ITuneLogger Log { get; }

  public void Dispose() {
    // Context.Dispose();
  }
}

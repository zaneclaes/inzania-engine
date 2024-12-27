#region

using System;
using IZ.Core.Auth;
using IZ.Core.Contexts;

#endregion

namespace IZ.Core.Observability.Logging;

public class TuneSpan : ITuneSpan {
  public ITuneContext Context { get; }

  public ITuneLogger Log { get; }

  public virtual void SetTag(string key, string value) { }

  public virtual void SetException(Exception ex) { }

  public virtual void SetSession(ITuneSession session) { }

  public virtual void Dispose() { }

  protected TuneSpan(ITuneContext context) {
    Context = context;
    Log = context.Log;
  }

  public static TuneSpan ForContext(ITuneContext context) => new TuneSpan(context);
}

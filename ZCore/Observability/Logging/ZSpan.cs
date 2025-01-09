#region

using System;
using IZ.Core.Auth;
using IZ.Core.Contexts;

#endregion

namespace IZ.Core.Observability.Logging;

public class ZSpan : IZSpan {
  public IZContext Context { get; }

  public IZLogger Log { get; }

  public virtual void SetTag(string key, string value) { }

  public virtual void SetException(Exception ex) { }

  public virtual void SetSession(IZSession session) { }

  public virtual void Dispose() { }

  protected ZSpan(IZContext context) {
    Context = context;
    Log = context.Log;
  }

  public static ZSpan ForContext(IZContext context) => new ZSpan(context);
}

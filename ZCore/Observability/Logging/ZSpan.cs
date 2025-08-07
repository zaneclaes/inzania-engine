#region

using System;
using IZ.Core.Auth;
using IZ.Core.Contexts;

#endregion

namespace IZ.Core.Observability.Logging;

public class ZSpan : IZSpan {

  protected ZSpan() {
    // Context = context;
    // Log = context.Log;
  }
  // public IZContext Context { get; }

  // public IZLogger Log { get; }

  public virtual void SetTag(string key, string value) { }

  public virtual void SetException(Exception ex) { }

  public virtual void SetSession(IZSession session) { }

  public virtual void Dispose() { }

  public static ZSpan ForContext() => new ZSpan();
}

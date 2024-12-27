#region

using System;
using IZ.Core.Auth;

#endregion

namespace IZ.Core.Contexts;

public interface ITuneSpan : IHaveContext, IDisposable {
  public void SetTag(string key, string value);

  public void SetException(Exception ex);

  public void SetSession(ITuneSession session);
}

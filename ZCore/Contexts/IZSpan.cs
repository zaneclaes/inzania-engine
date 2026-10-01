#region

using System;
using IZ.Core.Auth;

#endregion

namespace IZ.Core.Contexts;

public interface IZSpan : IDisposable {
  public void SetTag(string key, string value);

  public void SetException(Exception ex);

  /// <summary>Records a caught exception on the current trace without marking this span failed.</summary>
  public void RecordCaught(Exception ex, string? source = null);

  public void SetSession(IZSession session);
}

#region

using System.Collections.Generic;
using System.Diagnostics;

#endregion

namespace IZ.Core.Utils;

public class TuneTrace {
  public List<string> FilteredTrace { get; }

  public TuneTrace(string? stackTrace = null) {
    FilteredTrace = StackTraces.Filter(stackTrace ?? new StackTrace().ToString());
    if (stackTrace == null) FilteredTrace.RemoveAt(0);
  }

  public override string ToString() => string.Join("\n", FilteredTrace);
}

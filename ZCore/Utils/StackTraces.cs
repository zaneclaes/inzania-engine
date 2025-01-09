#region

using System;
using System.Collections.Generic;
using System.Linq;

#endregion

namespace IZ.Core.Utils;

public static class StackTraces {
  private static readonly string[] s_FilteredLogMessages = {
    @"UnityEngine.DebugLogHandler:Internal_Log", @"UnityEngine.DebugLogHandler:Log", @"UnityEngine.Logger:Log", @"UnityEngine.Debug", "System.Threading", "System.Runtime", "Serilog", "System.Text"
  };

  private static readonly string[] s_LastMessages = {
    @"System.Reflection.MonoMethod:InternalInvoke(Object, Object[], Exception&)", @"UnityEditor.TestTools.TestRunner.EditModeRunner:InvokeDelegator"
  };

  public static List<string> Filter(string inputStackTrace) {
    int idx;
    foreach (string? lastMessage in s_LastMessages) {
      idx = inputStackTrace.IndexOf(lastMessage, StringComparison.Ordinal);
      if (idx != -1)
        inputStackTrace = inputStackTrace.Substring(0, idx);
    }

    IEnumerable<string>? inputStackTraceLines = inputStackTrace
      .Split('\n').Select(t => t.Trim()).Where(s => s.StartsWith("at ")).Select(s => s.Substring(3));
    List<string> result = new List<string>();
    foreach (string? line in inputStackTraceLines) {
      if (s_FilteredLogMessages.Any(s => line.StartsWith(s)))
        continue;
      result.Add(line);
    }
    return result;
  }
}

#region

using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
#if Z_UNITY
using Lib.Logging;
#endif

#endregion

namespace IZ.Core.Utils;

public static class MachineHost {
  public static bool CheckRunningHost(string processName, int port = 5292) {
    Dictionary<long, string> procs = GetRunningHostProcesses(port);
    return procs.Values.Any(p => p.StartsWith(processName));
  }

  private static Dictionary<long, string> GetRunningHostProcesses(int port = 5292) {
    using Process p = new Process();
    p.StartInfo.UseShellExecute = false;
    p.StartInfo.RedirectStandardOutput = true;
    p.StartInfo.FileName = "lsof";
    p.StartInfo.Arguments = $"-i :{port}";
    p.Start();
    string txt = p.StandardOutput.ReadToEnd();
#if Z_UNITY
      UnityEngine.Debug.Log($"[PROCS] {txt}");
#endif
    Dictionary<long, string> output = txt.Trim().Split('\n')
      .Select(s => s.Trim()).Where(s => s.Contains("(LISTEN)"))
      .GroupBy(s => long.TryParse(s.Split(" ")[1], out long v) ? v : 0)
      .ToDictionary(g => g.Key, g => g.First().Split(" ").First());
    p.WaitForExit();
    p.Close();
    return output;
  }
}

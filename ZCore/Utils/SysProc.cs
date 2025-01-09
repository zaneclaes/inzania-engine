#region

using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using IZ.Core.Contexts;

#endregion

namespace IZ.Core.Utils;

public static class SysProc {
  public static async Task<bool> ExecuteProc(this IZContext context, string bin, string args = "", string? workDir = null, Action<string>? onLine = null) {
    string cmd = bin.Split('/').Last() + " " + args;
    using var process = new Process {
      StartInfo = {
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        CreateNoWindow = true,
        FileName = bin,
        Arguments = args
      },
      EnableRaisingEvents = true
    };
    if (workDir != null) process.StartInfo.WorkingDirectory = workDir;
    process.Exited += (sender, arg) => {
      var proc = (sender as Process)!;
      string wrn = proc.StandardError.ReadToEnd();
      if (!string.IsNullOrEmpty(wrn)) context.Log.Warning(wrn);
      context.Log.Information("[EXE] {cmd} exit code {code}", cmd, proc.ExitCode);
    };

    context.Log.Information("[EXE] {cmd}", cmd);
    try {
      process.Start();
      // string txt = p.StandardOutput.ReadToEnd();
      // string[] output = txt.Trim().Split('\n');
      // Log.Information("[EXE] execute {cmd}", cmd);
      while (!process.HasExited) {
        if (!process.StandardOutput.EndOfStream) {
          string? line = await process.StandardOutput.ReadLineAsync();
          if (line != null) {
            if (onLine != null) onLine.Invoke(line);
            else context.Log.Information("[EXE] {line}", line);
          }
        }
        await Task.Delay(100);
      }
      // Log.Information("[AWS] waiting for {cmd} to exit...");
      // process.WaitForExit();
      return true;
    } catch (Exception e) {
      context.Log.Error(e, "[EXE] {cmd} failed", cmd);
      // source.SetException(e);
      return false;
    }
  }
}

#region

using System;
using System.Diagnostics;
using System.Linq;
using System.IO;
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
    context.Log.Information("[EXE] {cmd}", cmd);
    try {
      if (!process.Start()) {
        context.Log.Error("[EXE] {cmd} did not start", cmd);
        return false;
      }

      // Both redirected pipes must drain from process start. Reading stderr from Exited while polling
      // stdout can deadlock when either pipe fills, and used to report every nonzero exit as success.
      Task<Exception?> stdout = DrainOutputAsync(process.StandardOutput, context, onLine);
      Task<Exception?> stderr = DrainErrorAsync(process.StandardError, context);
      await Task.WhenAll(WaitForExitAsync(process), stdout, stderr);

      Exception? drainFailure = stdout.Result ?? stderr.Result;
      if (drainFailure != null) {
        context.Log.Error(drainFailure, "[EXE] {cmd} output handling failed", cmd);
        return false;
      }
      context.Log.Information("[EXE] {cmd} exit code {code}", cmd, process.ExitCode);
      return process.ExitCode == 0;
    } catch (Exception e) {
      context.Log.Error(e, "[EXE] {cmd} failed", cmd);
      return false;
    }
  }

  private static async Task<Exception?> DrainOutputAsync(StreamReader reader, IZContext context, Action<string>? onLine) {
    Exception? failure = null;
    string? line;
    while ((line = await reader.ReadLineAsync()) != null) {
      try {
        if (onLine != null) onLine(line);
        else context.Log.Information("[EXE] {line}", line);
      } catch (Exception e) {
        // Keep draining after a callback failure so a child cannot remain blocked on a full pipe.
        failure ??= e;
      }
    }
    return failure;
  }

  private static async Task<Exception?> DrainErrorAsync(StreamReader reader, IZContext context) {
    string? line;
    while ((line = await reader.ReadLineAsync()) != null) context.Log.Warning("[EXE] {line}", line);
    return null;
  }

  private static Task WaitForExitAsync(Process process) {
    var source = new TaskCompletionSource<object?>();
    EventHandler? exited = null;
    exited = (_, _) => source.TrySetResult(null);
    process.Exited += exited;
    if (process.HasExited) source.TrySetResult(null);
    return source.Task;
  }
}

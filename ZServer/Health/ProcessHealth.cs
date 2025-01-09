#region

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using IZ.Core.Contexts;
using IZ.Core.Data;
using Microsoft.Extensions.Diagnostics.HealthChecks;

#endregion

namespace IZ.Server.Health;

public enum ProcessHealthState {
  Errored = 0,
  Ok = 1,
  Cpu,
  Memory
}

public class ProcessHealth : TransientObject, IHealthCheck {
  private static readonly DateTime StartTime = DateTime.Now;

  private string? _processOutput;

  public ProcessHealth(IZContext context) : base(context) { }

  public Task<HealthCheckResult> CheckHealthAsync(
    HealthCheckContext context, CancellationToken cancellationToken = new CancellationToken()
  ) {
    try {
      float memory = GetMemory();
      float cpu = GetCpu();
      Dictionary<string, object> data = new Dictionary<string, object> {
        {
          "memory", memory
        }, {
          "cpu", cpu
        }, {
          "pid", GetProcessId()
        }, {
          "uptime", GetUptime()
        }
      };

      // if (cpu > 80) return Task.FromResult(HealthCheckResult.Degraded(ProcessHealthState.Cpu.ToString(), data: data));
      // if (memory > 80) return Task.FromResult(HealthCheckResult.Degraded(ProcessHealthState.Memory.ToString(), data: data));
      return Task.FromResult(HealthCheckResult.Healthy(ProcessHealthState.Ok.ToString(), data));
    } catch (Exception e) {
      Log.Error(e, "[HEALTH] Process Health");
      return Task.FromResult(HealthCheckResult.Unhealthy(ProcessHealthState.Errored.ToString(), e));
    }
  }

  private uint GetProcessId() => (uint) Process.GetCurrentProcess().Id;

  private float GetMemory() {
    if (string.IsNullOrEmpty(GetProcessOutput())) return -1;
    return float.Parse(GetProcessOutput().Split(' ').LastOrDefault() ?? "-1");
  }

  private float GetCpu() {
    if (string.IsNullOrEmpty(GetProcessOutput())) return -1;
    return float.Parse(GetProcessOutput().Split(' ').FirstOrDefault() ?? "-1");
  }

  public double GetUptime() => DateTime.Now.Subtract(StartTime).TotalSeconds;

  private string GetProcessOutput() {
    if (_processOutput != null) return _processOutput;
    using Process p = new Process();
    p.StartInfo.UseShellExecute = false;
    p.StartInfo.RedirectStandardOutput = true;
    p.StartInfo.FileName = "ps";
    p.StartInfo.Arguments = $"-p {GetProcessId()} -o %cpu,%mem";
    p.Start();
    string? txt = p.StandardOutput.ReadToEnd();
    string[]? output = txt.Trim().Split('\n');
    p.WaitForExit();
    p.Close();
    return _processOutput = output.Length > 0 ? output[^1].Trim() : "";
  }
}

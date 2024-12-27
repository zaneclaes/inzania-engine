#region

using System.Threading;
using System.Threading.Tasks;
using IZ.Core.Contexts;
using IZ.Core.Data;
using Microsoft.Extensions.Diagnostics.HealthChecks;

#endregion

namespace IZ.Server.Health;

public class HostHealth : TransientObject, IHealthCheck {
  public HostHealth(ITuneContext context) : base(context) { }

  public Task<HealthCheckResult> CheckHealthAsync(
    HealthCheckContext context, CancellationToken cancellationToken = new CancellationToken()
  ) =>
    // try {
    //   HostNode node = await HostNodes.LoadBackgroundWorker(Context);
    //   Dictionary<string, object> data = new();
    //   if (node.HostState == HostState.Alive || node.HostState == HostState.Shutdown || node.HostState == HostState.Startup)
    //     return HealthCheckResult.Healthy(node.HostState.ToString(), data);
    //   return HealthCheckResult.Degraded(node.HostState.ToString(), data: data);
    // } catch (Exception e) {
    //   _context.Log.Error(e, "[HEALTH] Host Health");
    //   return HealthCheckResult.Unhealthy(HostState.Errored.ToString(), e);
    // }
    Task.FromResult(HealthCheckResult.Healthy("OK"));
}

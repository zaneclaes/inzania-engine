#region

using IZ.Core.Contexts;
using IZ.Core.Utils;
using IZ.Server.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

#endregion

namespace IZ.Server;

public static class HostingExtensions {

  public static IServiceCollection AddTuneServerCore<T>(
    this IServiceCollection collection, T tunealityApp
  ) where T : ZApp => collection
    .AddTunealityApp<T, HostContext>(tunealityApp)
    .AddLogging(lb => lb.AddSerilog())
    // .AddTuneQuery()
    //   .Services
    .AddHttpContextAccessor()
    .AddTransient<IProvideRootContext, HttpRootContextAccessor>()
    .AddSerilog();

  public static TOpts GetSectionOptions<TOpts>(this IConfiguration section, string name) where TOpts : new() {
    var ret = new TOpts();
    section.GetSection(name).Bind(name, ret);
    return ret;
  }

  public static ApplicationStorage ToTunealityApplicationDirectories(this IConfigurationSection dirs) {
    return new ApplicationStorage(
      dirs.GetSection("User").Value!,
      dirs.GetSection("Tmp").Value!,
      dirs.GetSection("wwwroot").Value);
  }
}

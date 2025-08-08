using IZ.Core.Utils;
using IZ.Core.Utils.ReCaptcha;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace IZ.Server.Utils.ReCaptcha;

public static class ReCaptchaExtensions {
  public static IServiceCollection AddReCaptcha(
    this IServiceCollection services, WebApplicationBuilder builder
  ) =>
    services
      .Configure<ReCaptchaOptions>(options => builder.Configuration.GetSection("ReCaptcha").Bind(options))
      .AddScoped<ReCaptchaValidator>();
}

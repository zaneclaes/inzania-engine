using IZ.Core.Utils;
using IZ.Server.Emails;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace IZ.Server.Utils.ReCaptcha;

public static class ReCaptchaExtensions {
  public static IServiceCollection AddReCaptcha(
    this IServiceCollection services, WebApplicationBuilder builder
  ) =>
    services
      .Configure<PublicSecretKey<ReCaptchaValidator>>(options => builder.Configuration.GetSection("ReCaptcha").Bind(options))
      .AddScoped<ReCaptchaValidator>()
  ;
}

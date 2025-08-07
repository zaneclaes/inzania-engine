using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace IZ.Server.Emails;

public static class EmailExtensions {
  public static IServiceCollection AddEmailSender<TDb>(
    this IServiceCollection services, WebApplicationBuilder builder
  ) where TDb : DbContext, IEmailSenderDb =>
    services
      .Configure<SendGridOptions>(options => builder.Configuration.GetSection("SendGrid").Bind(options))
      .AddScoped<SendGridSender, SendGridSender<TDb>>()
      .AddScoped<IEmailSender, EmailSender>();
}

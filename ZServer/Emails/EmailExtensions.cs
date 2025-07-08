using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IZ.Server.Emails;

public static class EmailExtensions {
  public static IServiceCollection AddEmailSender<TDb>(this IServiceCollection services) where TDb : DbContext, IEmailSenderDb =>
    services
      .AddScoped<SendGridSender, SendGridSender<TDb>>()
      .AddScoped<IEmailSender, EmailSender>()
    ;
}

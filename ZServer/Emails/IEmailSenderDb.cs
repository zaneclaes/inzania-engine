using Microsoft.EntityFrameworkCore;

namespace IZ.Server.Emails;

public interface IEmailSenderDb {
  public DbSet<EmailValidation> EmailValidations { get; }
}

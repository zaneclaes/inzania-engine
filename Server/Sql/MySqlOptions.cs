#region

using IZ.Data.Providers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

#endregion

namespace IZ.Server.Sql;

public class MySqlOptions : SqlConnectionOptions {

  public MySqlOptions(IConfigurationSection section) : base(section) {
    Version = new MySqlServerVersion(section.GetValue<string>("Version") ?? "8.0.34");
  }
  public MySqlServerVersion Version { get; }
}

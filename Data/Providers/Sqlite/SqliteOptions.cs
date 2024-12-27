#region

using Microsoft.Extensions.Configuration;

#endregion

namespace IZ.Data.Providers.Sqlite;

public class SqliteSettings : SqlConnectionOptions {
  public SqliteSettings(IConfigurationSection section) : base(section) { }
}

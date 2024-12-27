#region

using System.Collections.Generic;
using System.Linq;
using IZ.Data.Providers.Interceptors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

#endregion

namespace IZ.Data.Providers;

public class SqlConnectionOptions {

  public SqlConnectionOptions(IConfigurationSection section) {
    Connection = section.GetSection("Connection").GetChildren()
      .ToDictionary(ch => ch.Key, ch => ch.Value)!;
  }

  private Dictionary<string, string> Connection { get; }

  public string ToConnectionString(DbContextOptionsBuilder? options) {
    Dictionary<string, string>? settings = Connection.ToDictionary(a => a.Key, b => b.Value);
    if (settings.ContainsKey("UtcIntercept")) {
      if (options != null) options.AddInterceptors(new UtcTimeInterceptor());
      settings.Remove("UtcIntercept");
    }
    return string.Join("", settings.Select(ch => $"{ch.Key}={ch.Value};"));
  }
}

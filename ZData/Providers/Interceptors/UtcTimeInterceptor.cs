#region

using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

#endregion

namespace IZ.Data.Providers.Interceptors;

public class UtcTimeInterceptor : DbCommandInterceptor {
  public override InterceptionResult<DbDataReader> ReaderExecuting(
    DbCommand command,
    CommandEventData eventData,
    InterceptionResult<DbDataReader> result) {
    command.CommandText = $"SET time_zone = '+00:00'; {command.CommandText}";
    return result;
  }
}

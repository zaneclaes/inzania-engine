#region

using System;
using System.Threading.Tasks;
using IZ.Core.Contexts;

#endregion

namespace IZ.Core.Utils;

public static class AwsS3 {
  private static async Task Sync(IZContext context, string from, string to, string? flags = null) {
    if (!await context.ExecuteProc("/usr/local/bin/aws", $"s3 sync {from} {to}{flags ?? ""}", null, line => {
          if (!line.Contains("Completed")) context.Log.Information("[AWS] {line}", line);
        }))
      throw new SystemException("Failed to sync");
  }

}

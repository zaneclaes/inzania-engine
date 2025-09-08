using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using IZ.Core.Auth;
using IZ.Core.Contexts;
using IZ.Core.Data;
using IZ.Core.Data.Attributes;

namespace IZ.Core.Observability.Logging;

[ApiKey(nameof(ClientId), nameof(FileName), nameof(LineNumber))]
[ApiIndex(nameof(ClientId), nameof(FileName), nameof(LoggedAt))]
[ApiIndex(nameof(ByteSize), nameof(LoggedAt))]
public class LogEntry : DataObject {
  // public const int MaxMessageLength = 5000;
  //
  // public const int MaxExceptionLength = 2000;
  //
  // public const int MaxPropsLength = 8000;

  [MaxLength(Installation.ClientIdLength)] public string ClientId { get; set; } = null!;

  [MaxLength(255)] public string FileName { get; set; } = null!;

  public int LineNumber { get; set; }

  [MaxLength(255)] public string? UserId { get; set; } = null!;

  [MaxLength(32)] public string Level { get; set; } = null!;

  // [MaxLength(MaxMessageLength)]
  public string Message { get; set; } = null!;

  // [MaxLength(MaxExceptionLength)]
  public string? Exception { get; set; }

  // [MaxLength(MaxPropsLength)]
  public string Properties { get; set; } = null!;

  // Nume bytes used by strings, for help cleaning up later
  public ulong ByteSize { get; set; } = 0;

  public DateTime LoggedAt { get; set; }

  public static async Task CleanupData(IZContext context, DateTime? minAge = null, ulong minByteSize = 0, int limit = 1000) {
    minAge ??= DateTime.UtcNow -  TimeSpan.FromDays(30);
    var q = context.QueryFor<LogEntry>()
      .Filter(l => l.ByteSize >= minByteSize && l.LoggedAt < minAge);
    var entries = await q.Take(limit).LoadDataModelsAsync();
    var total = (long) entries.Count;
    if (total <= 0) return;
    if (total >= limit) total = await q.LoadCountAsync();
    context.Log.Information("[LOG ENTRY] cleaning up {cnt} / {total}", entries, total);
    await context.Data.RemoveAsync(entries.ToArray());
    await context.Data.SaveAsync();
  }
}

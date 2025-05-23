using System;
using System.ComponentModel.DataAnnotations;
using IZ.Core.Data;
using IZ.Core.Data.Attributes;

namespace IZ.Core.Observability.Logging;

[ApiKey(nameof(InstallId), nameof(FileName), nameof(LineNumber))]
[ApiIndex(nameof(InstallId), nameof(FileName), nameof(LoggedAt))]
public class LogEntry : DataObject {
  public const int MaxMessageLength = 5000;

  public const int MaxPropsLength = 10000;

  [MaxLength(255)] public string InstallId { get; set; } = null!;

  [MaxLength(255)] public string FileName { get; set; } = null!;

  public int LineNumber { get; set; }

  [MaxLength(255)] public string? UserId { get; set; } = null!;

  [MaxLength(32)] public string Level { get; set; } = null!;

  [MaxLength(MaxMessageLength)] public string Message { get; set; } = null!;

  [MaxLength(MaxPropsLength)] public string Properties { get; set; } = null!;

  public DateTime LoggedAt { get; set; }
}

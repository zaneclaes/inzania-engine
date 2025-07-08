#region

using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using IZ.Core.Data;
using Newtonsoft.Json;

#endregion

namespace IZ.Server.Emails;

public class EmailValidation : ITimeStampData {

  [Key] [MaxLength(255)] public string Email { get; set; } = default!;

  [MaxLength(128)] public string Verdict { get; set; } = default!;

  public decimal Score { get; set; }

  [MaxLength(128)] public string Local { get; set; } = default!;

  [MaxLength(128)] public string Host { get; set; } = default!;

  [MaxLength(255)] public string? Suggestion { get; set; }

  [MaxLength(255)] public string? Source { get; set; }

  [JsonProperty(PropertyName = "ip_address")]
  [MaxLength(255)]
  public string? IpAddress { get; set; }

  [NotMapped]
  public EmailValidationVerdict ValidationVerdict =>
    Enum.TryParse(Verdict, true, out EmailValidationVerdict verdict) ? verdict : EmailValidationVerdict.Unknown;

  [NotMapped] public bool IsInvalid => ValidationVerdict <= EmailValidationVerdict.Invalid;

  public DateTime? UpdatedAt { get; set; }

  public DateTime CreatedAt { get; set; }
}

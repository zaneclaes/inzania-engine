using System;
using System.Text.Json.Serialization;

namespace IZ.Core.Utils.ReCaptcha;

public class ReCaptchaResponse {
  [JsonPropertyName("success")]
  public bool Success { get; set; }

  [JsonPropertyName("score")]
  public float Score { get; set; }

  [JsonPropertyName("action")]
  public string Action { get; set; } = null!;

  [JsonPropertyName("challenge_ts")]
  public DateTime ChallengeTs { get; set; }

  [JsonPropertyName("hostname")]
  public string Hostname { get; set; } = null!;

  [JsonPropertyName("error-codes")]
  public string[] ErrorCodes { get; set; } = null!;
}

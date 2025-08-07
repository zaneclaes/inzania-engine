using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using IZ.Core.Contexts;
using IZ.Core.Utils;
using IZ.Core.Utils.ReCaptcha;
using Microsoft.Extensions.Options;

namespace IZ.Server.Utils.ReCaptcha;

public class ReCaptchaValidator : LogicBase {

  private readonly PublicSecretKey<ReCaptchaValidator> _opts;

  public ReCaptchaValidator(IZContext ctx, IOptions<PublicSecretKey<ReCaptchaValidator>> opts) : base(ctx) {
    _opts = opts.Value;
  }
  public string PublicKey => _opts.PublicKey;

  public async Task<float> VerifyReCaptchaAsync(string token) {
    string? secret = _opts.SecretKey;
    if (string.IsNullOrWhiteSpace(secret)) secret = Environment.GetEnvironmentVariable($"{Context.App.ProductName.ToUpperInvariant()}_RECAPTCHA_V3_SECRET");
    if (string.IsNullOrWhiteSpace(secret)) throw new NullReferenceException("ReCaptchaV3.Secret");
    var http = new HttpClient();
    var content = new FormUrlEncodedContent(new[] {
      new KeyValuePair<string, string>("secret", secret), new KeyValuePair<string, string>("response", token)
    });

    var response = await http.PostAsync("https://www.google.com/recaptcha/api/siteverify", content);
    string json = await response.Content.ReadAsStringAsync();

    var result = JsonSerializer.Deserialize<ReCaptchaResponse>(json);
    return result?.Success == true ? result.Score : -1f;
  }
}

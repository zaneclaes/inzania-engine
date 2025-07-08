namespace IZ.Core.Utils;

public class PublicSecretKey<TService> where TService : class {
  public string PublicKey { get; set; } = null!;
  public string SecretKey { get; set; } = null!;
}

using IZ.Core.Data.Attributes;

namespace IZ.Core.Utils;

public abstract class PublicSecretKey {
  public string PublicKey { get; set; } = null!;
  [ApiSecret] public string SecretKey { get; set; } = null!;
}

public class PublicSecretKey<TService> : PublicSecretKey where TService : class { }

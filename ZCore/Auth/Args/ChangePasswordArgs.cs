using IZ.Core.Data;

namespace IZ.Core.Auth.Args;

public class ChangePasswordArgs : TransientObject {
  public string UserId { get; set; } = null!;

  public string? Token { get; set; } = null!;

  public string? Password { get; set; } = null!;
}

public class AuthChangePasswordArgs {
  public string UserId { get; set; } = null!;

  public string? Token { get; set; } = null!;

  public string? Password { get; set; } = null!;
}

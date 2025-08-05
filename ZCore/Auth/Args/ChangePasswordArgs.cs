using IZ.Core.Data;

namespace IZ.Core.Auth.Args;

public class ChangePasswordArgs : TransientObject, IAmOwned {
  public string UserId { get; set; } = null!; // can also be username or email

  public string? Token { get; set; } = null!;

  public string? Password { get; set; } = null!;
}

public class AuthChangePasswordArgs {
  public string UserId { get; set; } = null!; // can also be username or email

  public string? Token { get; set; } = null!;

  public string? Password { get; set; } = null!;
}

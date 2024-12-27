#region

using IZ.Core.Data;

#endregion

namespace IZ.Core.Auth.Args;

public class SignUpArgs : TransientObject {
  public string Email { get; set; } = default!;

  public string Username { get; set; } = default!;

  public string Password { get; set; } = default!;
}

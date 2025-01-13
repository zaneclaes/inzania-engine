#region

using IZ.Core.Data;

#endregion

namespace IZ.Core.Auth.Args;

public class SignUpArgs : TransientObject {
  public string Email { get; set; } = null!;

  public string Username { get; set; } = null!;

  public string Password { get; set; } = null!;
}

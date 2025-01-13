#region

using IZ.Core.Data;

#endregion

namespace IZ.Core.Auth.Args;

public class LoginArgs : TransientObject {
  public string Username { get; set; } = null!;

  public string Password { get; set; } = null!;
}

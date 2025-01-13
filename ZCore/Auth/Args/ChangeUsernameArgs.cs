#region

using IZ.Core.Data;

#endregion

namespace IZ.Core.Auth.Args;

public class ChangeUsernameArgs : TransientObject {
  public string UserId { get; set; } = null!;

  public string Username { get; set; } = null!;
}

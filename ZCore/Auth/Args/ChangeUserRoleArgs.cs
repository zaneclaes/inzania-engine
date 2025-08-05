#region

using IZ.Core.Data;

#endregion

namespace IZ.Core.Auth.Args;

public class ChangeUserRoleArgs : TransientObject, IAmOwned {
  public string UserId { get; set; } = null!;

  public ZUserRole Role { get; set; }
}

public class AuthChangeUserRoleArgs {
  public string UserId { get; set; } = null!;

  public ZUserRole Role { get; set; }
}

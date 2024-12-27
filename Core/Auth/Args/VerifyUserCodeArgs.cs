#region

using IZ.Core.Data;

#endregion

namespace IZ.Core.Auth.Args;

public class VerifyUserCodeArgs : TransientObject {
  public string UserId { get; set; } = default!;

  public string Code { get; set; } = default!;
}

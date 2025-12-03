using IZ.Core.Data;
using IZ.Core.Data.Attributes;

namespace IZ.Core.Auth;

public class LastLoginInfo  : TransientObject {
  [Observable] public string UserId { get; set; } = null!;
  [Observable] public string Username { get; set; } = null!;
  public ZUserRole Role { get; set; }
}

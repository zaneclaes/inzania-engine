using IZ.Core.Data;
using IZ.Core.Data.Attributes;

namespace IZ.Core.Auth;

public class StoredSession : TransientObject {
  [Observable] public string UserId { get; set; } = null!;
  [Observable] public string Username { get; set; } = null!;
  public string AccessToken { get; set; } = null!;
  [Observable] public ZUserRole UserRole { get; set; }

  public override string ToString() => $"<#{UserId} ({Username}) [{UserRole}]>";
}

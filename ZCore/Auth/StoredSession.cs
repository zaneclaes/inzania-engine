using IZ.Core.Data;

namespace IZ.Core.Auth;

public class StoredSession : TransientObject {
  public string UserId { get; set; } = null!;
  public string Username { get; set; } = null!;
  public string AccessToken { get; set; } = null!;
  public ZUserRole UserRole { get; set; }
}

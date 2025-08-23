#region

using System.ComponentModel.DataAnnotations;
using IZ.Core.Data;

#endregion

namespace IZ.Core.Auth.Args;

public class LoginArgs : TransientObject {
  public string Username { get; set; } = null!;

  public string Password { get; set; } = null!;
}

public class AuthLoginArgs {
  [Required]
  [StringLength(32, ErrorMessage = "Username must be 4-32 characters long.", MinimumLength = 4)]
  public string Username { get; set; } = null!;

  [Required]
  public string Password { get; set; } = null!;
}

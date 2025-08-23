#region

using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using IZ.Core.Data;

#endregion

namespace IZ.Core.Auth.Args;

public class SignUpArgs : TransientObject {
  public string Email { get; set; } = null!;

  public string Username { get; set; } = null!;

  public string Password { get; set; } = null!;
}

public class AuthSignUpArgs {
  [Required]
  [EmailAddress]
  public string Email { get; set; } = null!;

  [Required]
  [StringLength(32, ErrorMessage = "Username must be 4-32 characters long.", MinimumLength = 4)]
  public string Username { get; set; } = null!;

  [Required]
  [StringLength(32, ErrorMessage = "Password must be 6-32 characters long.", MinimumLength = 6)]
  public string Password { get; set; } = null!;

  [Required]
  [Compare(nameof(Password), ErrorMessage = "Passwords do not match.")]
  public string PasswordAgain { get; set; } = null!;
}

namespace IZ.Core.Auth;

public enum ZPolicy {
  None, // full public
  VirtualUser,
  UnconfirmedUser,
  ConfirmedUser,
  Subscriber,
  ProtectedUser,
  Moderator,
  Admin,
  CurrentUser
}

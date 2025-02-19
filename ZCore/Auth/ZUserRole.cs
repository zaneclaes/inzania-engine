#region

using System.Collections.Generic;
using System.Linq;
using IZ.Core.Data.Attributes;

#endregion

namespace IZ.Core.Auth;

public enum ZUserRole {
  Visitor = 0,
  [ApiDocs("New user, has not confirmed email")] Unconfirmed = 1,
  User = 3,
  Subscriber = 5,
  Moderator = 7,
  Admin = 10
}

public static class ZRoles {
  public static List<ZUserRole> AllRoles { get; } = new List<ZUserRole> {
    ZUserRole.Visitor,
    ZUserRole.Unconfirmed,
    ZUserRole.User,
    ZUserRole.Subscriber,
    ZUserRole.Moderator,
    ZUserRole.Admin
  };

  public static List<ZUserRole> GetRoles(ZUserRole minimumLevel) =>
    AllRoles.Where(r => r >= minimumLevel).ToList();

  public static string[] GetRoleNames(ZUserRole minimumLevel) =>
    GetRoles(minimumLevel).Select(r => r.ToString()).ToArray();
}

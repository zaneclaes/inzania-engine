using System;
using System.Linq;

namespace IZ.Core.Auth;

public interface IOwned { }

public interface IMightBeOwned : IOwned {
  public string? UserId { get; }
}

public interface IAmOwned : IOwned {
  public string UserId { get; }
}

public interface IAmOwned<TUser> : IAmOwned {
  public TUser User { get; set; }
}

public static class OwnershipExtensions {

  private static string? CheckUserRole(this IZUser? user, ZUserRole minRole, params string[] bypassIds) {
    if (user == null) return nameof(user);
    if (user.Role >= minRole) return null;
    if (!bypassIds.Contains(user.Id)) return user.Role.ToString() + " User";
    return null;
  }

  public static string? GetOwnerId(this IOwned owned) {
    if (owned is IAmOwned own) return own.UserId;
    if (owned is IMightBeOwned o) return o.UserId;
    throw new SystemException($"{owned.GetType()} is IOwned, but not IAmOwned or IMightBeOwned");
  }

  // public static string? CheckOwnershipException(this IOwned owned, IZIdentity? id, ZUserRole bypassRole = ZUserRole.Admin) =>
  //   owned.CheckOwnershipException(id?.IZUser, bypassRole);

  // public static string? CheckOwnershipException(this IOwned owned, IZUser? user, ZUserRole bypassRole = ZUserRole.Admin) {
  //   string? ownerId = owned.GetOwnerId();
  //   return ownerId == null ? CheckUserRole(user, bypassRole) : CheckUserRole(user, bypassRole, ownerId);
  // }

  public static void GetOwnershipException(this IAmOwned owned, IZIdentity? id, ZUserRole bypassRole = ZUserRole.Admin) =>
    owned.EnsureOwnership(id?.IZUser, bypassRole);

  public static void EnsureOwnership(this IAmOwned owned, IZIdentity? id, ZUserRole bypassRole = ZUserRole.Admin) =>
    owned.EnsureOwnership(id?.IZUser, bypassRole);

  public static void EnsureOwnership(this IAmOwned owned, IZUser? user, ZUserRole bypassRole = ZUserRole.Admin) {
    string? exception = CheckUserRole(user, bypassRole, owned.UserId);
    if (exception != null) throw new UnauthorizedAccessException(exception);
  }

  public static void EnsureReadOwnership(this IMightBeOwned owned, IZIdentity? id, ZUserRole bypassRole = ZUserRole.Admin) =>
    owned.EnsureOwnership(true, id?.IZUser, bypassRole);

  public static void EnsureReadOwnership(this IMightBeOwned owned, IZUser? id, ZUserRole bypassRole = ZUserRole.Admin) =>
    owned.EnsureOwnership(true, id, bypassRole);

  public static void EnsureWriteOwnership(this IMightBeOwned owned, IZIdentity? id, ZUserRole bypassRole = ZUserRole.Admin) =>
    owned.EnsureOwnership(false, id?.IZUser, bypassRole);

  public static void EnsureWriteOwnership(this IMightBeOwned owned, IZUser? id, ZUserRole bypassRole = ZUserRole.Admin) =>
    owned.EnsureOwnership(false, id, bypassRole);

  private static void EnsureOwnership(this IMightBeOwned owned, bool allowUnowned, IZUser? user, ZUserRole bypassRole = ZUserRole.Admin) {
    var exception = GetOwnershipException(owned.UserId, allowUnowned, user, bypassRole);
    if (exception != null) throw new UnauthorizedAccessException(exception);
  }

  public static string? GetOwnershipException(string? uid, bool allowUnowned, IZUser? user, ZUserRole bypassRole = ZUserRole.Admin) {
    string? exception = null;
    if (string.IsNullOrWhiteSpace(uid)) {
      if (!allowUnowned) exception = CheckUserRole(user, bypassRole);
    } else {
      exception = CheckUserRole(user, bypassRole, uid);
    }
    return exception;
  }
}

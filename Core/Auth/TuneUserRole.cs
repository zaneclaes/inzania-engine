#region

using System.Collections.Generic;
using System.Linq;
using IZ.Core.Data.Attributes;

#endregion

namespace IZ.Core.Auth;

public enum TuneUserRole {
  Visitor = 0,
  [ApiDocs("New user, has not confirmed email")] Unconfirmed = 1,
  User = 3,
  Subscriber = 5,
  Moderator = 7,
  Admin = 10
}

public static class TuneRoles {
  public static List<TuneUserRole> AllRoles { get; } = new List<TuneUserRole> {
    TuneUserRole.Visitor,
    TuneUserRole.Unconfirmed,
    TuneUserRole.User,
    TuneUserRole.Subscriber,
    TuneUserRole.Moderator,
    TuneUserRole.Admin
  };

  public static List<TuneUserRole> GetRoles(TuneUserRole minimumLevel) =>
    AllRoles.Where(r => r >= minimumLevel).ToList();

  public static string[] GetRoleNames(TuneUserRole minimumLevel) =>
    GetRoles(minimumLevel).Select(r => r.ToString()).ToArray();

  // public static List<TuneRole> UserRoles { get; } = new List<TuneRole>() {
  //   TuneRole.User, TuneRole.Subscriber, TuneRole.Moderator, TuneRole.Admin
  // };
  //
  // public static List<TuneRole> SubscriberRoles { get; } = new List<TuneRole>() {
  //   TuneRole.Subscriber, TuneRole.Moderator, TuneRole.Admin
  // };
  //
  // public static List<TuneRole> ModeratorRoles { get; } = new List<TuneRole>() {
  //   TuneRole.Moderator, TuneRole.Admin
  // };
  //
  // public static List<TuneRole> AdminRoles { get; } = new List<TuneRole>() {
  //   TuneRole.Admin
  // };
}

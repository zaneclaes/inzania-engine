#region

using System;

#endregion

namespace IZ.Core.Utils;

public static class DateTimeUtils {
  public static uint GetDayNumber(this DateTime currentDate, string? timeZone) {
    if (timeZone != null) {
      // TODO: convert date to timezone
    }
    return (uint) Math.Floor((currentDate - DateTime.UnixEpoch).TotalDays);
  }

  public static string ToSortableString(this DateTime date, string joiner = "") => string.Join(joiner, new string[] {
    date.Year.ToString("D4"), date.Month.ToString("D2"), date.Day.ToString("D2"),
  });

  public static double GetUnixTimestampSec(this DateTime dt) => dt.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;
}

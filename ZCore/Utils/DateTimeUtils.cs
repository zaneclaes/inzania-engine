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

  public static DateTime ToDateTimeUnixUtc(this long unixTimestamp) => new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc).AddSeconds(unixTimestamp);

  public static int GetSortableYMD(this DateTime dt) => dt.Year * 10000 + dt.Month * 100 + dt.Day;

  public static string ToAgeHoursDays (this TimeSpan t) {
    if (t.TotalDays <= 1) {
      int th = (int) (t.TotalHours);
      return $"{th} hour" + (th != 1 ? "s" : "");
    }

    int td = (int) t.TotalDays;
    return $"{td} day" + (td > 1 ? "s" : "");
  }
}

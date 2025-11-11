#region

using System;

#endregion

namespace IZ.Core.Utils;

public static class DateTimeUtils {
  public static string GetTimeZoneString(this TimeZoneInfo tz) {
    var desc = tz.DisplayName;
    if (desc.StartsWith("(UTC") && desc.EndsWith(")")) return desc;
    return $"(UTC{tz.BaseUtcOffset}) {tz.StandardName.Replace("Standard ", "")} ({tz.Id})";
  }

  public static TimeSpan GetTimeZoneOffset(string? timeZone) {
    int hrOffset = 0;
    if (timeZone != null && timeZone.StartsWith("(UTC")) {
      var hrStr = timeZone.Substring("(UTC".Length).Split(':')[0];
      int.TryParse(hrStr, out hrOffset);
    }
    return TimeSpan.FromHours(hrOffset);
  }

  public static DateTime LocalizeForTimeZone(this DateTime utcInstant, string? timeZone) {
    if (utcInstant.Kind != DateTimeKind.Utc)
      utcInstant = DateTime.SpecifyKind(utcInstant, DateTimeKind.Utc);

    return utcInstant + GetTimeZoneOffset(timeZone);
  }

  public static uint GetDayNumber(this DateTime utcInstant, string? timeZone) {
    if (utcInstant.Kind != DateTimeKind.Utc)
      utcInstant = DateTime.SpecifyKind(utcInstant, DateTimeKind.Utc);

    var local = utcInstant.LocalizeForTimeZone(timeZone);
    var localMidnightUtc = local.Date.LocalizeForTimeZone(timeZone);

    return (uint) Math.Floor((localMidnightUtc - DateTime.UnixEpoch).TotalDays);
  }

  public static string ToSortableString(this DateTime date, string joiner = "") => string.Join(joiner, date.Year.ToString("D4"), date.Month.ToString("D2"), date.Day.ToString("D2"));

  public static double GetUnixTimestampSec(this DateTime dt) => dt.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;

  public static DateTime ToDateTimeUnixUtc(this long unixTimestamp) => new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc).AddSeconds(unixTimestamp);

  public static int GetSortableYMD(this DateTime dt) => dt.Year * 10000 + dt.Month * 100 + dt.Day;

  public static string ToAgeHoursDays(this TimeSpan t) {
    if (t.TotalDays <= 1) {
      int th = (int) t.TotalHours;
      return $"{th} hour" + (th != 1 ? "s" : "");
    }

    int td = (int) t.TotalDays;
    return $"{td} day" + (td > 1 ? "s" : "");
  }
}

#region

using System;
using System.Collections.Generic;

#endregion

namespace IZ.Core.Utils;

public static class DateTimeUtils {
  // A user's time zone travels as the display string every client platform can produce
  // ("(UTC-07:00) Mountain Time (Denver)"), so that string is all a conversion has to work with.
  // Localizing with the "-07:00" *inside* it is wrong twice over: it is the zone's BASE offset, so it
  // is an hour out for the half of the year the zone spends on DST, and an hour-only parse drops the
  // ":30" of India, Adelaide and the rest. Resolve the string back to a real TimeZoneInfo instead and
  // ask that for the offset at the instant being converted. Platforms with no time-zone database
  // (Unity WebGL) resolve nothing and fall back to parsing the offset out of the string.
  static readonly Dictionary<string, TimeZoneInfo?> zoneCache = new();

  public static string GetTimeZoneString(this TimeZoneInfo tz) {
    var desc = tz.DisplayName;
    if (desc.StartsWith("(UTC") && desc.EndsWith(")")) return desc;
    return $"(UTC{tz.BaseUtcOffset}) {tz.StandardName.Replace("Standard ", "")} ({tz.Id})";
  }

  /// <summary>The zone a <see cref="GetTimeZoneString" /> names, or null where it cannot be resolved.</summary>
  public static TimeZoneInfo? ResolveTimeZone(string? timeZone) {
    if (string.IsNullOrWhiteSpace(timeZone)) return null;

    lock (zoneCache) {
      if (zoneCache.TryGetValue(timeZone!, out var cached)) return cached;
      var found = FindTimeZone(timeZone!);
      zoneCache[timeZone!] = found;
      return found;
    }
  }

  static TimeZoneInfo? FindTimeZone(string timeZone) {
    try {
      // The usual form is a verbatim DisplayName...
      foreach (var tz in TimeZoneInfo.GetSystemTimeZones())
        if (tz.DisplayName == timeZone) return tz;

      // ...while the form GetTimeZoneString synthesizes ends in the zone's own id.
      var open = timeZone.LastIndexOf('(');
      if (open >= 0 && timeZone.EndsWith(")")) {
        var id = timeZone.Substring(open + 1, timeZone.Length - open - 2);
        if (id.Length > 0) return TimeZoneInfo.FindSystemTimeZoneById(id);
      }
    } catch (Exception) {
      // No time-zone database (WebGL), or an id this platform does not carry: fall back to the offset.
    }

    return null;
  }

  public static TimeSpan GetTimeZoneOffset(string? timeZone) => GetTimeZoneOffset(timeZone, DateTime.UtcNow);

  public static TimeSpan GetTimeZoneOffset(string? timeZone, DateTime utcInstant) {
    var tz = ResolveTimeZone(timeZone);
    if (tz != null) return tz.GetUtcOffset(DateTime.SpecifyKind(utcInstant, DateTimeKind.Utc));
    return ParseUtcOffset(timeZone);
  }

  /// <summary>The offset literally written in a "(UTC±hh:mm)" prefix — the base offset, so DST-blind.</summary>
  static TimeSpan ParseUtcOffset(string? timeZone) {
    if (timeZone == null || !timeZone.StartsWith("(UTC")) return TimeSpan.Zero;

    var close = timeZone.IndexOf(')');
    var body = (close < 0
      ? timeZone.Substring("(UTC".Length)
      : timeZone.Substring("(UTC".Length, close - "(UTC".Length)).Trim();
    if (body.Length == 0) return TimeSpan.Zero;

    var sign = body[0] == '-' ? -1 : 1;
    if (body[0] == '-' || body[0] == '+') body = body.Substring(1);

    var parts = body.Split(':');
    int.TryParse(parts[0], out var hours);
    var minutes = 0;
    if (parts.Length > 1) int.TryParse(parts[1], out minutes);

    return TimeSpan.FromMinutes(sign * (hours * 60 + minutes));
  }

  public static DateTime LocalizeForTimeZone(this DateTime utcInstant, string? timeZone) {
    if (utcInstant.Kind != DateTimeKind.Utc)
      utcInstant = DateTime.SpecifyKind(utcInstant, DateTimeKind.Utc);

    return utcInstant + GetTimeZoneOffset(timeZone, utcInstant);
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

  public static string ToAgeHoursDays(this DateTime utcDateTime) {
    var age = DateTime.UtcNow - utcDateTime;
    return age.ToAgeHoursDays();
  }
}

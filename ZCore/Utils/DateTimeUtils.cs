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

  public static double GetUnixTimestampSec(this DateTime dt) => dt.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;
}

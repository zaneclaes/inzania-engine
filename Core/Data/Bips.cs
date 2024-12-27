#region

using System;
using IZ.Core.Data.Attributes;

#endregion
// 0-10,000 percentage value (see Bips)

namespace IZ.Core.Data;

[ApiDocs("Expresses a percentage/score value as 0-10,000 value, used for storage & transmission of doubles with reasonable precision")]
public static class Bips {
  public const double BIP_DOUBLE = 10000.0;

  public const Bip BIP_MAX = 10000;

  // public static int ToFractionRound(Bip val) => (int)Math.Round(ToFraction(val));

  // Generic add/subtract methods defaults to clamping from 0-100% range...
  public static Bip Subtract(Bip val, long other, Bip min = 0, Bip max = BIP_MAX) =>
    val < 0 ? Add((short) -val, other, min, max) : Clamp(val - other, min, max);
  public static Bip Add(Bip val, long other, Bip min = 0, Bip max = BIP_MAX) =>
    val < 0 ? Subtract((short) -val, other, min, max) : Clamp(val + other, min, max);

  // SCORE add/subtract method allows for negative score
  public static Bip SubtractScore(Bip val, long other) => Subtract(val, other, short.MinValue, short.MaxValue);
  public static Bip AddScore(Bip val, long other) => Add(val, other, short.MinValue, short.MaxValue);
  public static Bip ClampScore(Bip val) => Clamp(val, short.MinValue, short.MaxValue);

  // n.b., clamp function is an INT to avoid overflows in testing size
  public static Bip Clamp(long val, long min = 0, long max = BIP_MAX) =>
    (Bip) Math.Min(Math.Max(Math.Max(min, Bip.MinValue), val), Math.Min(max, Bip.MaxValue));

  public static Bip Clamp(double val, long min = 0, long max = BIP_MAX) => Clamp((Bip) Math.Round(val), min, max);

  public static double ToFraction(Bip val) => Math.Min(Math.Max(val, -BIP_DOUBLE), BIP_DOUBLE) / BIP_DOUBLE;
  public static Bip FromFraction(double val) => (short) Math.Floor(val * BIP_DOUBLE);

  public static int ToPercent(Bip val) => val / 100;
  public static Bip FromPercent(int pct) => (short) (pct * 100);

  public static string FractionPct(double val, int decimals = 2) {
    if (decimals <= 0) return ((int) (val * 100)).ToString();
    if (decimals >= 2) return $"{val * 100.0:0.00}";
    return $"{val * 100.0:0.0}";
  }
  public static string BipPct(Bip val, int decimals = 2) => FractionPct(ToFraction(val), decimals);
}

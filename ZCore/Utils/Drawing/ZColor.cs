using System;

namespace IZ.Core.Utils.Drawing;

public readonly struct ZColor : IEquatable<ZColor> {
  public readonly byte R;
  public readonly byte G;
  public readonly byte B;
  public readonly byte A;

  public ZColor(byte r, byte g, byte b, byte a = 255) {
    R = r;
    G = g;
    B = b;
    A = a;
  }

  public static ZColor FromRgb(byte r, byte g, byte b) => new ZColor(r, g, b);
  public static ZColor FromRgba(byte r, byte g, byte b, byte a) => new ZColor(r, g, b, a);

  public ZColor WithAlpha(byte alpha) => new ZColor(R, G, B, alpha);

  public static ZColor FromHex(string hex) {
    if (hex.StartsWith("#")) hex = hex.Substring(1);
    if (hex.Length == 6) {
      return new ZColor(
        Convert.ToByte(hex.Substring(0, 2), 16),
        Convert.ToByte(hex.Substring(2, 2), 16),
        Convert.ToByte(hex.Substring(4, 2), 16)
      );
    }
    if (hex.Length == 8) {
      return new ZColor(
        Convert.ToByte(hex.Substring(0, 2), 16),
        Convert.ToByte(hex.Substring(2, 2), 16),
        Convert.ToByte(hex.Substring(4, 2), 16),
        Convert.ToByte(hex.Substring(6, 2), 16)
      );
    }
    throw new ArgumentException("Invalid hex format. Use #RRGGBB or #RRGGBBAA.");
  }

  public string ToHex(bool includeAlpha = false) => includeAlpha
    ? $"#{R:X2}{G:X2}{B:X2}{A:X2}"
    : $"#{R:X2}{G:X2}{B:X2}";

  public override string ToString() => $"Color(R={R}, G={G}, B={B}, A={A})";

  public bool Equals(ZColor other) => R == other.R && G == other.G && B == other.B && A == other.A;
  public override bool Equals(object? obj) => obj is ZColor c && Equals(c);
  public override int GetHashCode() => HashCode.Combine(R, G, B, A);

  public static bool operator ==(ZColor left, ZColor right) => left.Equals(right);
  public static bool operator !=(ZColor left, ZColor right) => !left.Equals(right);

  // Common colors
  public static readonly ZColor White = new ZColor(255, 255, 255);
  public static readonly ZColor Black = new ZColor(0, 0, 0);
  public static readonly ZColor Red = new ZColor(255, 0, 0);
  public static readonly ZColor Green = new ZColor(0, 255, 0);
  public static readonly ZColor Blue = new ZColor(0, 0, 255);
  public static readonly ZColor Transparent = new ZColor(0, 0, 0, 0);
}

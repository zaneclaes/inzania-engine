using System;

namespace IZ.Core.Utils.Drawing;

public class ZSize {

  public ZSize(float x, float y) {
    Width = x;
    Height = y;
  }

  public ZSize(double x, double y) {
    Width = (float) x;
    Height = (float) y;
  }

  public float Width { get; set; }

  public float Height { get; set; }

  public ZSize WithScale(float scale) => new ZSize(Width * scale, Height * scale);

  public ZSize Rounded() => new ZSize(Math.Round(Width), Math.Round(Height));

  public override string ToString() => $"<{Width}x{Height}>";
}

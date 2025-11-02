namespace IZ.Core.Utils.Drawing;

public class ZPoint {
  public float X { get; set; }

  public float Y { get; set; }

  public ZPoint(float x, float y) {
    X = x;
    Y = y;
  }

  public ZPoint Clone() => new ZPoint(X, Y);

  public override string ToString() => $"<x={X},y={Y}>";
}

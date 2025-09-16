namespace IZ.Core.Utils.Drawing;

public class ZSize {

  public ZSize(float x, float y) {
    Width = x;
    Height = y;
  }
  public float Width { get; set; }

  public float Height { get; set; }

  public override string ToString() => $"<{Width}x{Height}>";
}

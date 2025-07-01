namespace IZ.Core.Utils.Drawing ;

public struct ZRect {
  public float X { get; }
  public float Y { get; }
  public float Width { get; }
  public float Height { get; }

  public ZRect(float x, float y, float width, float height)
  {
    X = x;
    Y = y;
    Width = width;
    Height = height;
  }
}

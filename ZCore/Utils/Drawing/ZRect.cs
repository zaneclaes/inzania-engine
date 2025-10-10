using System;

namespace IZ.Core.Utils.Drawing;

public class ZRect {
  public ZPoint Position { get; set; } = new ZPoint(0, 0);
  public float X {
    get => Position.X;
    set => Position.X = value;
  }
  public float Y {
    get => Position.Y;
    set => Position.Y = value;
  }

  public ZSize Size { get; set; } = new ZSize(0, 0);
  public float Width {
    get => Size.Width;
    set => Size.Width = value;
  }
  public float Height {
    get => Size.Height;
    set => Size.Height = value;
  }

  public float Right => X + Width;
  public float Top => Y + Height;

  public ZRect(float x = 0f, float y = 0f, float width = 0f, float height = 0f) {
    X = x;
    Y = y;
    Width = width;
    Height = height;
  }

  public ZRect WithY(float newY) => new ZRect(X, newY, Width, Height);

  public ZRect WithScaledSize(float scale) => new ZRect(X, Y, Width * scale, Height * scale);

  public bool OverlapsWithWidth(ZRect other) => X < other.Right && other.X < Right;
  public bool OverlapsWithHeight(ZRect other) => Y < other.Top && other.Y < Top;

  public bool OverlapsWith(ZRect other) => OverlapsWithWidth(other) && OverlapsWithHeight(other);

  public void SetSize(ZSize size) {
    Width = size.Width;
    Height = size.Height;
  }

  public void SetPosition(ZSize size) {
    Width = size.Width;
    Height = size.Height;
  }

  public override string ToString() => $"<Rect @{X}x{Y} {Width}x{Height}>";
}

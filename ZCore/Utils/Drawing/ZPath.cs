using System;
using System.Collections.Generic;
using System.Linq;

namespace IZ.Core.Utils.Drawing;

public class ZPath {
  private readonly List<List<ZPoint>> _contours = new List<List<ZPoint>>();
  private List<ZPoint>? _currentContour;

  public ZRect Bounds { get; private set; }

  public void MoveTo(float x, float y) {
    _currentContour = new List<ZPoint> {
      new ZPoint(x, y)
    };
    _contours.Add(_currentContour);
    UpdateBounds();
  }

  public void LineTo(float x, float y) {
    EnsureStarted();
    _currentContour!.Add(new ZPoint(x, y));
    UpdateBounds();
  }

  public void QuadTo(float cx, float cy, float x, float y, int segments = 20) {
    EnsureStarted();

    var p0 = _currentContour!.Last();
    ZPoint p1 = new ZPoint(cx, cy);
    ZPoint p2 = new ZPoint(x, y);

    for (int i = 1; i <= segments; i++) {
      float t = i / (float) segments;
      float u = 1 - t;
      float qx = u * u * p0.X + 2 * u * t * p1.X + t * t * p2.X;
      float qy = u * u * p0.Y + 2 * u * t * p1.Y + t * t * p2.Y;
      _currentContour.Add(new ZPoint(qx, qy));
    }

    UpdateBounds();
  }

  public void Close() {
    if (_currentContour == null || _currentContour.Count < 2)
      return;

    var first = _currentContour[0];
    var last = _currentContour[^1];
    if (Math.Abs(first.X - last.X) > float.Epsilon || Math.Abs(first.Y - last.Y) > float.Epsilon){
      _currentContour.Add(first);
    }
    UpdateBounds();
  }

  public IEnumerable<IEnumerable<ZPoint>> GetContours() => _contours;

  private void EnsureStarted() {
    if (_currentContour == null)
      throw new InvalidOperationException("You must call MoveTo() before LineTo() or QuadTo().");
  }

  private void UpdateBounds() {
    IEnumerable<ZPoint>? allPoints = _contours.SelectMany(c => c).ToList();
    if (!allPoints.Any()) {
      Bounds = new ZRect(0, 0, 0, 0);
      return;
    }

    float minX = allPoints.Min(p => p.X);
    float minY = allPoints.Min(p => p.Y);
    float maxX = allPoints.Max(p => p.X);
    float maxY = allPoints.Max(p => p.Y);

    Bounds = new ZRect(minX, minY, maxX - minX, maxY - minY);
  }
}

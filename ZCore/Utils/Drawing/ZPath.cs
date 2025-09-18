using System;
using System.Collections.Generic;
using System.Linq;

namespace IZ.Core.Utils.Drawing;

public class ZPath {
  private readonly List<List<ZPoint>> _contours = new List<List<ZPoint>>();
  private List<ZPoint>? _currentContour;

  private List<ZPoint> _boundsPoints = new List<ZPoint>();

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

  public void CubicTo(float cx1, float cy1,
    float cx2, float cy2,
    float x,   float y,
    int segments = 30)
  {
    EnsureStarted();

    var p0 = _currentContour!.Last();          // start (previous point)
    var p1 = new ZPoint(cx1, cy1);             // control 1
    var p2 = new ZPoint(cx2, cy2);             // control 2
    var p3 = new ZPoint(x, y);                 // end

    // _boundsPoints.Add(p1);
    // _boundsPoints.Add(p2);
    // _boundsPoints.Add(p3);

    for (int i = 1; i <= segments; i++) {
      float t = i / (float)segments;
      float u = 1f - t;

      // Cubic Bézier: (1-t)^3 P0 + 3(1-t)^2 t P1 + 3(1-t) t^2 P2 + t^3 P3
      float qx =
        u*u*u * p0.X +
        3f*u*u*t * p1.X +
        3f*u*t*t * p2.X +
        t*t*t * p3.X;

      float qy =
        u*u*u * p0.Y +
        3f*u*u*t * p1.Y +
        3f*u*t*t * p2.Y +
        t*t*t * p3.Y;

      _currentContour!.Add(new ZPoint(qx, qy));
    }

    UpdateBounds();
  }


  public void QuadTo(float cx, float cy, float x, float y, int segments = 20) {
    EnsureStarted();

    var p0 = _currentContour!.Last();
    var p1 = new ZPoint(cx, cy);
    var p2 = new ZPoint(x, y);

    // _boundsPoints.Add(p1);
    // _boundsPoints.Add(p2);

    for (int i = 1; i <= segments; i++) {
      float t = i / (float) segments;
      float u = 1 - t;
      float qx = u * u * p0.X + 2 * u * t * p1.X + t * t * p2.X;
      float qy = u * u * p0.Y + 2 * u * t * p1.Y + t * t * p2.Y;
      _currentContour!.Add(new ZPoint(qx, qy));
    }

    UpdateBounds();
  }

  public void Close() {
    if (_currentContour == null || _currentContour.Count < 2)
      return;

    var first = _currentContour[0];
    var last = _currentContour[^1];
    if (Math.Abs(first.X - last.X) > float.Epsilon || Math.Abs(first.Y - last.Y) > float.Epsilon) {
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
    List<ZPoint> allPoints = _contours.SelectMany(c => c).ToList();
    allPoints.AddRange(_boundsPoints);
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

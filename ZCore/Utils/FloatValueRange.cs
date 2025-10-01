namespace IZ.Core.Utils;

public class FloatValueRange : ValueRange<float> {
  public FloatValueRange(float minimum, float maximum) {
    Minimum = minimum;
    Maximum = maximum;
  }
}

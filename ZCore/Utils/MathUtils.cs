using System;
using System.Linq;

namespace IZ.Core.Utils;

public static class MathUtils {

  // Helper: Greatest Common Divisor
  private static int Gcd(int a, int b) {
    while (b != 0) {
      int temp = b;
      b = a % b;
      a = temp;
    }
    return Math.Abs(a);
  }

  // Helper: Least Common Multiple of two numbers
  private static int Lcm(int a, int b) {
    if (a == 0 || b == 0) return 0;
    return Math.Abs(a / Gcd(a, b) * b);
  }

  // LCM of an array
  public static int Lcm(params int[] numbers) {
    if (numbers == null || numbers.Length == 0)
      throw new ArgumentException("At least one number is required");

    return numbers.Aggregate(Lcm);
  }
}

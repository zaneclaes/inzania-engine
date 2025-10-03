using System;
using System.Linq;

namespace IZ.Core.Utils;

public static class MathUtils {

  // Helper: Greatest Common Divisor
  private static long Gcd(long a, long b) {
    while (b != 0) {
      long temp = b;
      b = a % b;
      a = temp;
    }
    return Math.Abs(a);
  }

  // Helper: Least Common Multiple of two numbers
  private static long Lcm(long a, long b) {
    if (a == 0 || b == 0) return 0;
    return Math.Abs(a / Gcd(a, b) * b);
  }

  // LCM of an array
  public static long Lcm(params long[] numbers) {
    if (numbers == null || numbers.Length == 0)
      throw new ArgumentException("At least one number is required");

    return numbers.Aggregate(Lcm);
  }
}

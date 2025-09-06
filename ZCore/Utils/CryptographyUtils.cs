#region

using System;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;

#endregion

namespace IZ.Core.Utils;

public static class CryptographyUtils {

  // Encoding as base62 provides the shortest possible ALPHANUMERIC length
  private const string Base62Alphabet = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
  public static string ToMd5Hash(this string str) =>
    BitConverter.ToString(MD5.Create().ComputeHash(Encoding.UTF8.GetBytes(str))).Replace("-", string.Empty);

  public static string ToSha256String(this string input) {
    if (string.IsNullOrWhiteSpace(input)) return string.Empty;

    using (var sha = SHA256.Create()) {
      byte[] bytes = Encoding.UTF8.GetBytes(input);
      byte[] hash = sha.ComputeHash(bytes);

      return Convert.ToBase64String(hash);
    }
  }

  // A secure hashing function with a predictable length; the max length is 48, but it can be auto-truncated
  public static string ToSecureAlphanumericHash(this string input, string key, int? length = null) {
    using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(key));
    byte[] hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(input));
    string base62 = Base62Encode(hash);
    return length == null ? base62 : base62.Substring(0, Math.Min(length.Value, base62.Length));
  }
  private static string Base62Encode(byte[] data) {
    // Convert hash bytes to BigInteger (unsigned, little-endian)
    var value = new BigInteger(data.Append((byte) 0).ToArray()); // prevent sign bit issues

    var sb = new StringBuilder();
    while (value > 0) {
      value = BigInteger.DivRem(value, 62, out var remainder);
      sb.Insert(0, Base62Alphabet[(int) remainder]);
    }

    return sb.ToString();
  }

  public static string ToBase62String(this string str) => Base62Encode(Encoding.UTF8.GetBytes(str));
  public static string ToBase64String(this string str) => Convert.ToBase64String(Encoding.UTF8.GetBytes(str));

  // public static ulong ToSimpleHashVal(this string str) => XXHash.Hash64(Encoding.UTF8.GetBytes(str));
  //
  // public static string ToSimpleHashStr(this string str) => str.ToSimpleHashVal().ToString("X");

  public static string ToChecksum(this byte[] str) {
    using var cryptoProvider = SHA1.Create();
    return BitConverter.ToString(cryptoProvider.ComputeHash(str));
  }

  public static string ToChecksum(this string str) => Encoding.UTF8.GetBytes(str).ToChecksum();
}

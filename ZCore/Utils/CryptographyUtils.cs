#region

using System;
using System.Security.Cryptography;
using System.Text;
using IZ.Core.Utils.Cryptography;

#endregion

namespace IZ.Core.Utils;

public static class CryptographyUtils {
  public static string ToMd5Hash(this string str) =>
    BitConverter.ToString(MD5.Create().ComputeHash(Encoding.UTF8.GetBytes(str))).Replace("-", string.Empty);

  public static string ToSha256String(this string input) {
    if (string.IsNullOrWhiteSpace(input)) return string.Empty;

    using (var sha = SHA256.Create())
    {
      var bytes = Encoding.UTF8.GetBytes(input);
      var hash = sha.ComputeHash(bytes);

      return Convert.ToBase64String(hash);
    }
  }

  public static string ToBase64String(this string str) => Convert.ToBase64String(Encoding.UTF8.GetBytes(str));

  public static ulong ToSimpleHashVal(this string str) => XXHash.Hash64(Encoding.UTF8.GetBytes(str));

  public static string ToSimpleHashStr(this string str) => str.ToSimpleHashVal().ToString("X");

  public static string ToChecksum(this byte[] str) {
    using var cryptoProvider = SHA1.Create();
    return BitConverter.ToString(cryptoProvider.ComputeHash(str));
  }

  public static string ToChecksum(this string str) => Encoding.UTF8.GetBytes(str).ToChecksum();
}

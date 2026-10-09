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
    Md5.ComputeHashString(str).ToUpperInvariant().Replace("-", string.Empty);
    // BitConverter.ToString(MD5.Create().ComputeHash(Encoding.UTF8.GetBytes(str))).Replace("-", string.Empty);

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

#if !Z_UNITY
  /// <summary>A bounded, portable digest of regular files. Links are never followed.</summary>
  public static string DirectorySha256(string root, int maxFiles = 4000, long maxBytes = 256L * 1024 * 1024) {
    if (maxFiles < 1 || maxBytes < 1) throw new ArgumentOutOfRangeException(nameof(maxFiles));
    root = System.IO.Path.GetFullPath(root);
    if (new System.IO.DirectoryInfo(root).LinkTarget != null)
      throw new System.IO.IOException("Digest root must not be a link");
    var paths = new System.Collections.Generic.List<string>();
    long bytes = 0;
    int entries = 0;
    void Visit(string directory, int depth) {
      if (depth > 32) throw new System.IO.IOException("Digest payload exceeds its depth bound");
      foreach (var entry in new System.IO.DirectoryInfo(directory).EnumerateFileSystemInfos()) {
        if (++entries > maxFiles * 2L) throw new System.IO.IOException("Digest payload exceeds its entry bound");
        if (entry.LinkTarget != null) throw new System.IO.IOException("Digest payload contains a link");
        if (entry is System.IO.DirectoryInfo child) Visit(child.FullName, depth + 1);
        else if (entry is System.IO.FileInfo file) {
          paths.Add(file.FullName);
          bytes = checked(bytes + file.Length);
          if (paths.Count > maxFiles || bytes > maxBytes) throw new System.IO.IOException("Digest payload exceeds its bounds");
        }
      }
    }
    Visit(root, 0);
    using var digest = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
    long readBytes = 0;
    foreach (var file in paths.OrderBy(file => System.IO.Path.GetRelativePath(root, file).Replace('\\', '/'), StringComparer.Ordinal)) {
      string relative = System.IO.Path.GetRelativePath(root, file).Replace('\\', '/');
      digest.AppendData(Encoding.UTF8.GetBytes(relative + "\0"));
      using var stream = System.IO.File.OpenRead(file);
      using var fileDigest = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
      var buffer = new byte[65536];
      int count;
      while ((count = stream.Read(buffer, 0, buffer.Length)) != 0) {
        readBytes = checked(readBytes + count);
        if (readBytes > maxBytes) throw new System.IO.IOException("Digest payload grew beyond its byte bound");
        fileDigest.AppendData(buffer, 0, count);
      }
      digest.AppendData(fileDigest.GetHashAndReset());
    }
    return Convert.ToHexString(digest.GetHashAndReset());
  }
#endif
}

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

  public static ulong ToSimpleHashVal(this string str) => XXHash.Hash64(Encoding.UTF8.GetBytes(str));

  public static string ToSimpleHashStr(this string str) => str.ToSimpleHashVal().ToString("X");
}

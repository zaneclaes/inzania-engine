#region

using System.IO;

#endregion

namespace IZ.Core.Utils;

public class FilePaths {
  public static string GetAbsolutePath(string directory) {
    string cur = Directory.GetCurrentDirectory();
    while (directory.StartsWith("..")) {
      cur = Directory.GetParent(cur)!.FullName;
      directory = directory.Substring(3);
    }
    return Path.Combine(cur, directory);
  }
}

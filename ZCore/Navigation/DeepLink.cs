#region

using System;
using System.Linq;
using IZ.Core.Contexts;
using IZ.Core.Data;
using IZ.Core.Data.Attributes;

#endregion

namespace IZ.Core.Navigation;

public class DeepLink : TransientObject {

  protected readonly string? _path;

  public readonly string Scheme;

  public readonly string? Hash;

  public readonly string? QueryString;

  [Observable] public string[] Parts { get; }

  public string[] SubPaths => Parts.Length > 1 ? Parts.Skip(1).ToArray() : new string[] { };

  public string Path => string.Join("/", Parts) + (string.IsNullOrWhiteSpace(Hash) ? "" : $"#{Hash}");

  public DeepLink(IZContext context, string path) : base(context) {
    var schemes = path.Split("://");
    Scheme = schemes.Length > 1 ? schemes[0] : ZEnv.ProductName.ToLowerInvariant();

    var hashes = schemes.Last().Split("#");
    Hash = hashes.Length > 1 ? hashes[1] : null;

    var qps = hashes.First().Split("?");
    QueryString = qps.Length > 1 ? qps[1] : null;

    _path = qps.First().Trim('/').ToLower();
    Parts = _path.Split('/').Where(p => !string.IsNullOrWhiteSpace(p)).ToArray();
  }

  public string ToDeepLinkUrl() => Scheme + "://" + Path;

  public string ToWebUrl() => Context.App.Url + "/" + Path;

  public string FirstPart => GetPart(0) ?? "";

  public string? GetPart(int index) => Parts.Length > index ? Parts[index] : null;

  public override string ToString() => ToDeepLinkUrl() + $" ({_path})";
}

public abstract class DeepLink<TPage> : DeepLink where TPage : SitePage {

  protected DeepLink(IZContext context, string path) : base(context, path) {
    Page = context.GetRequiredService<Sitemap>().GetPagePath<TPage>(string.Join("/", Parts));
    // if (!IsValid) {
    //   var p = string.Join("/", Parts);
    //   if (p != "not-found")
    //    Log.Warning("[DL] invalid page {section}", p);
    // }
  }
  // private static string Schema => ZEnv.ProductName.ToLower();

  public bool IsValid => Page != null;

  [Observable] public TPage? Page { get; }

  public bool IsInCategory(string category) =>
    FirstPart.Equals(category, StringComparison.InvariantCultureIgnoreCase);

  // Only returns an object if the path has components
  // public static DeepLink? FromPath(IZContext context, string? path) {
  //   if (path == null) return null;
  //   var dl = new DeepLink(context, path);
  //   return dl.IsValid ? dl : null;
  // }

  // public static DeepLink ForSong(IZContext context, string? relPath = null) => FromCategory(context, SiteCategory.Songs, relPath ?? "");
  //
  // public static DeepLink ForMusicTheory(IZContext context, string? relPath = null) => FromCategory(context, SiteCategory.Home, relPath ?? "");
}

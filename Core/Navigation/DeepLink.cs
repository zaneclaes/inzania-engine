#region

using System.Linq;
using IZ.Core.Contexts;
using IZ.Core.Data;
using IZ.Core.Data.Attributes;
using IZ.Core.Utils;

#endregion

namespace IZ.Core.Navigation;

public class DeepLink : TransientObject {
  private static string Schema => IZEnv.ProductName.ToLower();

  public bool IsValid => Page != null;

  [Observable] public SitePage? Page { get; }

  private readonly string? _path;

  [Observable] public string[] Parts { get; }

  public SiteCategory Category => Page?.Category ?? SiteCategory.Unknown;

  private DeepLink(ITuneContext context, string path) : base(context) {
    // var hi = path.IndexOf('#');
    // var qu = path.IndexOf('?');
    //

    _path = path.Split("://").Last().Split("#").First().Split("?").First().Trim('/').ToLower();
    Parts = _path.Split('/').Where(p => !string.IsNullOrWhiteSpace(p)).ToArray();
    Page = context.App.Sitemap.GetPage(string.Join("/", Parts));
    if (!IsValid) {
      Log.Warning("[DL] invalid page {section}", string.Join("/", Parts));
    }
    // PageTitle = (Section == DeepLinkSection.Home ? "" : (Section + " | ")) + TuneEnv.ProductName;
  }

  public string ToUrl() => Schema + "://" + string.Join("/", Parts);

  public override string ToString() => ToUrl() + $" ({_path})";

  // Only returns an object if the path has components
  public static DeepLink? FromPath(ITuneContext context, string? path) {
    if (path == null) return null;
    var dl = new DeepLink(context, path);
    return dl.IsValid ? dl : null;
  }

  public static DeepLink ForSong(ITuneContext context, string? relPath = null) => FromPath(context,
    $"{SiteCategory.Songs.ToKebabCase()}/{relPath ?? ""}")!;

  public static DeepLink ForMusicTheory(ITuneContext context, string? relPath = null) => FromPath(context,
    $"{SiteCategory.Home.ToKebabCase()}/{relPath ?? ""}")!;
}

#region

using System.Linq;
using IZ.Core.Contexts;
using IZ.Core.Data;
using IZ.Core.Data.Attributes;
using IZ.Core.Utils;
using Microsoft.Extensions.DependencyInjection;

#endregion

namespace IZ.Core.Navigation;

public class DeepLink : TransientObject {
  private static string Schema => ZEnv.ProductName.ToLower();

  public bool IsValid => Page != null;

  [Observable] public SitePage? Page { get; }

  private readonly string? _path;

  [Observable] public string[] Parts { get; }

  public SiteCategory Category => Page?.Category ?? SiteCategory.Unknown;

  private DeepLink(IZContext context, string path) : base(context) {
    _path = path.Split("://").Last().Split("#").First().Split("?").First().Trim('/').ToLower();
    Parts = _path.Split('/').Where(p => !string.IsNullOrWhiteSpace(p)).ToArray();
    Page = context.GetRequiredService<Sitemap>().GetPage(string.Join("/", Parts));
    if (!IsValid) {
      Log.Warning("[DL] invalid page {section}", string.Join("/", Parts));
    }
  }

  public string ToUrl() => Schema + "://" + string.Join("/", Parts);

  public override string ToString() => ToUrl() + $" ({_path})";

  // Only returns an object if the path has components
  public static DeepLink? FromPath(IZContext context, string? path) {
    if (path == null) return null;
    var dl = new DeepLink(context, path);
    return dl.IsValid ? dl : null;
  }

  public static DeepLink ForSong(IZContext context, string? relPath = null) => FromPath(context,
    $"{SiteCategory.Songs.ToKebabCase()}/{relPath ?? ""}")!;

  public static DeepLink ForMusicTheory(IZContext context, string? relPath = null) => FromPath(context,
    $"{SiteCategory.Home.ToKebabCase()}/{relPath ?? ""}")!;
}

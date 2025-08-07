#region

using System;
using System.Linq;
using HotChocolate.Utilities;
using IZ.Core.Contexts;
using IZ.Core.Data;
using IZ.Core.Data.Attributes;
using IZ.Core.Utils;
using Microsoft.Extensions.DependencyInjection;

#endregion

namespace IZ.Core.Navigation;

public abstract class DeepLink<TPage> : TransientObject where TPage : SitePage {
  private static string Schema => ZEnv.ProductName.ToLower();

  public bool IsValid => Page != null;

  [Observable] public TPage? Page { get; }

  private readonly string? _path;

  [Observable] public string[] Parts { get; }

  public bool IsInCategory(string category) =>
    FirstPart.Equals(category, StringComparison.InvariantCultureIgnoreCase);

  public string FirstPart => GetPart(0) ?? "";

  public string Path => string.Join("/", Parts);

  public string? GetPart(int index) => Parts.Length > index ? Parts[index] : null;

  protected DeepLink(IZContext context, string path) : base(context) {
    _path = path.Split("://").Last().Split("#").First().Split("?").First().Trim('/').ToLower();
    Parts = _path.Split('/').Where(p => !string.IsNullOrWhiteSpace(p)).ToArray();
    Page = context.GetRequiredService<Sitemap>().GetPagePath<TPage>(string.Join("/", Parts));
    if (!IsValid) {
      Log.Warning("[DL] invalid page {section}", string.Join("/", Parts));
    }
  }

  public string ToUrl() => Schema + "://" + Path;

  public override string ToString() => ToUrl() + $" ({_path})";

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

#region

using System.Linq;
using IZ.Core.Contexts;
using IZ.Core.Data;
using IZ.Core.Data.Attributes;

#endregion

namespace IZ.Core.Navigation;

public class CurrentPage<TPage, TLink, TMap> : TransientObject
  where TPage : SitePage
  where TLink : DeepLink<TPage>
  where TMap : Sitemap<TPage, TLink> {
  public CurrentPage(IZContext context, string path, TMap sitemap, TLink? deepLink) : base(context) {
    // Nav = nav;
    Path = path;
    Sitemap = sitemap;
    DeepLink = deepLink; // TuneDeepLink.FromPath(context, Path);
  }
  // public NavigationManager Nav { get; private set; }

  public TMap Sitemap { get; }

  [Observable] public TLink? DeepLink { get; }

  public TPage? SitePage => DeepLink?.Page;

  [Observable] public string Path { get; }

  public void SendPageView() {
    string title = SitePage?.Title ?? $"{Path.Split("/").First()}";
    // Context.Log.Information("[GA] {path} => {title}", Page?.Path ?? Path, title);
    Context.Analytics?.PageView(SitePage?.Path ?? Path, title);
  }
}

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

  public TMap Sitemap { get; }

  [Observable] public TLink? DeepLink { get; }

  public TPage? SitePage => DeepLink?.Page;

  [Observable] public string Path { get; }

  private ISitePageContent? _content;

  protected CurrentPage(IZContext context, string path, TMap sitemap, TLink? deepLink) : base(context) {
    // Nav = nav;
    Path = path;
    Sitemap = sitemap;
    DeepLink = deepLink; // TuneDeepLink.FromPath(context, Path);
    _content = SitePage?.GetContent(new DeepLink(Context, path).SubPaths);
  }
  // public NavigationManager Nav { get; private set; }

  public void SetContent(ISitePageContent? content) {
    _content = content;
  }

  public void SendPageView() {
    var title = _content?.Title ?? SitePage?.Title ?? $"{Path.Split("/").First()}";
    // Context.Log.Information("[GA] {path} => {title}", Page?.Path ?? Path, title);
    Context.Analytics?.PageView(SitePage?.Path ?? Path, title);
  }
}

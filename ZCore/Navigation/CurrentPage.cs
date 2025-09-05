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

  public TPage? TopPage => DeepLink?.Page;

  public ISitePage? Content => (_content as ISitePage) ?? TopPage;

  [Observable] public string Title => Content?.Title ?? $"{Path.Split("/").First()}";

  [Observable] public string Path { get; }

  private ISiteSubPage? _content;

  protected CurrentPage(IZContext context, string path, TMap sitemap, TLink? deepLink) : base(context) {
    // Nav = nav;
    Path = path;
    Sitemap = sitemap;
    DeepLink = deepLink; // TuneDeepLink.FromPath(context, Path);
    _content = TopPage?.GetContent(new DeepLink(Context, path).SubPaths);
  }
  // public NavigationManager Nav { get; private set; }

  public void SetContent(ISiteSubPage? content) {
    _content = content;
  }

  public void SendPageView() {
    // Context.Log.Information("[GA] {path} => {page}", Path, Title);
    Context.Analytics?.PageView(TopPage?.Path ?? Path, Title);
  }

  public override string ToString() => $"<{Title} {Path} />";
}
